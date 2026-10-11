use chrono::{FixedOffset, NaiveDate, NaiveDateTime, TimeZone, Utc};
use chrono_tz::Tz;
use serde::{Deserialize, Serialize};
use std::collections::HashMap;
use std::path::PathBuf;
use std::sync::Mutex;
use std::time::{SystemTime, UNIX_EPOCH};
use tauri::Manager;

const HTTP_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(30);

/// Bounds the TCP dial and the proxy CONNECT handshake only, not the session.
const IMAP_CONNECT_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(30);

/// Open an IMAP-over-TLS connection, through the Windows proxy when one applies
/// (netproxy SPEC [PX-08]).
///
/// Replaces `imap::connect`, which dials its own `TcpStream` and so could never
/// be proxied: reqwest and ureq negotiate their own tunnels, but IMAP brings a
/// bare socket, and mail sync simply failed on a proxied network. This does
/// what `imap::connect` does - dial, TLS, greeting - over a socket that may be
/// an HTTP CONNECT tunnel instead of a direct connection. Once established the
/// tunnel is transparent, so TLS is layered identically either way.
fn imap_connect(
    host: &str,
    port: u16,
) -> Result<imap::Client<native_tls::TlsStream<std::net::TcpStream>>, String> {
    let tls = native_tls::TlsConnector::builder()
        .build()
        .map_err(|e| format!("TLS error: {}", e))?;
    let sock = widget_netproxy::connect_tcp(host, port, IMAP_CONNECT_TIMEOUT)?;
    let stream = tls
        .connect(host, sock)
        .map_err(|e| format!("TLS handshake failed for {}:{}: {}", host, port, e))?;
    let mut client = imap::Client::new(stream);
    client
        .read_greeting()
        .map_err(|e| format!("IMAP greeting failed ({}:{}): {}", host, port, e))?;
    Ok(client)
}

/// Shared HTTP client with a 30s timeout (1.2 / 1.20). One connection-pooled
/// `reqwest::Client` (cheap to clone, since it's `Arc` inside) instead of a fresh
/// client per request, and critically, a timeout so a stalled endpoint can't
/// hang a request forever. Honors the Windows proxy (netproxy SPEC [PX-01]).
///
/// Used by every path here, including the user-URL ones (caldav/carddav/github).
/// Those previously built their own client, justified by needing a custom
/// user-agent - but five of the six set no user-agent at all, so they were just
/// duplicates that each had to be remembered separately whenever the client
/// configuration changed. RSS is the one real exception; see `http_client_ua`.
fn http_client() -> reqwest::Client {
    use std::sync::OnceLock;
    static CLIENT: OnceLock<reqwest::Client> = OnceLock::new();
    CLIENT
        .get_or_init(|| {
            widget_netproxy::client_builder(HTTP_TIMEOUT)
                .build()
                .unwrap_or_else(|e| {
                    log::error!("email: HTTP client build failed ({e}); no proxy support");
                    reqwest::Client::new()
                })
        })
        .clone()
}

/// Like `http_client`, but with a custom user-agent. Some feed hosts reject the
/// default, so RSS identifies itself.
fn http_client_ua(user_agent: &'static str) -> reqwest::Client {
    widget_netproxy::client_builder(HTTP_TIMEOUT)
        .user_agent(user_agent)
        .build()
        .unwrap_or_else(|e| {
            log::error!("email: HTTP client build failed ({e}); no proxy support");
            reqwest::Client::new()
        })
}

/// GET a bearer-authenticated API URL, retrying on a throttle (429) or
/// transient unavailability (503). Graph throttles bursts: several widgets
/// (calendar/email/contacts) hitting Graph on dashboard load routinely trip the
/// per-app limit, which previously surfaced as a hard "Graph API error 429"
/// that a manual reload "fixed". We now honor the server's `Retry-After`
/// (bounded) and retry, so the transient burst self-heals.
///
/// Not Graph-specific: Google's APIs throttle the same way and answer with the
/// same `Retry-After`, so the Google callers here share this one path.
///
/// This is server-directed waiting, not wall-clock polling: we wait exactly as
/// long as Graph asks (clamped to `MAX_RETRY_WAIT` so a hostile/garbage header
/// can never hang the request), at most `MAX_RETRIES` times. When `Retry-After`
/// is absent we fall back to a small exponential backoff (1s, 2s, 4s). Only the
/// idempotent GET is retried. Non-throttle responses (200, 401, 403, …) return
/// on the first attempt, so every caller's existing status mapping is unchanged.
async fn get_with_retry(
    client: &reqwest::Client,
    url: &str,
    token: &str,
    prefer: Option<&str>,
) -> Result<reqwest::Response, reqwest::Error> {
    const MAX_RETRIES: u32 = 3;
    const MAX_RETRY_WAIT: std::time::Duration = std::time::Duration::from_secs(30);

    let mut attempt: u32 = 0;
    loop {
        let mut req = client.get(url).bearer_auth(token);
        if let Some(p) = prefer {
            req = req.header("Prefer", p);
        }
        let resp = req.send().await?;

        let status = resp.status().as_u16();
        let throttled = status == 429 || status == 503;
        if !throttled || attempt >= MAX_RETRIES {
            return Ok(resp);
        }

        let wait = retry_after_delay(&resp)
            .unwrap_or_else(|| std::time::Duration::from_secs(1u64 << attempt))
            .min(MAX_RETRY_WAIT);
        attempt += 1;
        tokio::time::sleep(wait).await;
    }
}

/// [S-63] Sentinel for a transport-level failure, localized by the frontend.
/// Kept in sync with `widgets/office/todo/Todo.tsx` / `TodoConfig.tsx`.
const NETWORK_ERROR: &str = "network_error";

/// [S-63] Turn a transport-level `reqwest` failure into that sentinel, after
/// logging what actually went wrong.
///
/// `reqwest`'s own `Display` is "error sending request for url (…)": it names
/// the URL but not the cause — DNS failure, TLS reject, connection reset and
/// timeout all print that one sentence, with the real reason only in the
/// `source` chain. Shown verbatim ([S-06]) it reads as an unexplained crash,
/// in English, to a user who may not read English. So the widget gets a
/// localizable sentinel and the full cause chain goes to the log, where it can
/// actually be diagnosed. Issue nwalser/themia-releases#113.
fn net_error(what: &str, e: &reqwest::Error) -> String {
    use std::error::Error as _;
    let mut chain = e.to_string();
    let mut src = e.source();
    while let Some(s) = src {
        chain.push_str(" <- ");
        chain.push_str(&s.to_string());
        src = s.source();
    }
    log::warn!("{what}: network failure: {chain}");
    NETWORK_ERROR.to_string()
}

/// Parse a `Retry-After` response header. Graph/Gmail send delta-seconds
/// (`"120"`); we accept that form. An absent or unparseable value yields `None`
/// so the caller falls back to exponential backoff. (HTTP-date form is rare for
/// these APIs and not relied upon.)
fn retry_after_delay(resp: &reqwest::Response) -> Option<std::time::Duration> {
    let raw = resp
        .headers()
        .get(reqwest::header::RETRY_AFTER)?
        .to_str()
        .ok()?;
    let secs: u64 = raw.trim().parse().ok()?;
    Some(std::time::Duration::from_secs(secs))
}

const CLIENT_ID: &str = "6cf22c0f-103a-4f8a-ae35-178d62575897";
const TENANT: &str = "common";
const SCOPE: &str = "User.Read Mail.ReadWrite Calendars.Read Contacts.Read Tasks.ReadWrite Files.Read offline_access";

// Google OAuth (installed-app / desktop client, PKCE flow)
// Register a "Desktop app" OAuth 2.0 Client in Google Cloud Console. Google
// requires its "secret" on token exchange/refresh even for Desktop clients
// using PKCE, and it is not confidential for installed apps since it ships in
// the binary. It still stays out of the public source [S-53]: official builds
// read it at compile time from THEMIA_GOOGLE_CLIENT_SECRET (the release script
// loads it from .env), and a build without it refuses Google sign-in up front.
const GOOGLE_CLIENT_ID: &str =
    "187748979097-url067h00d18pjbjgskddssdj0nrq04s.apps.googleusercontent.com";
const GOOGLE_CLIENT_SECRET: Option<&str> = option_env!("THEMIA_GOOGLE_CLIENT_SECRET");
const GOOGLE_SCOPE: &str = "openid email profile \
    https://www.googleapis.com/auth/gmail.readonly \
    https://www.googleapis.com/auth/calendar.events.readonly \
    https://www.googleapis.com/auth/calendar.calendarlist.readonly \
    https://www.googleapis.com/auth/tasks \
    https://www.googleapis.com/auth/contacts.readonly";

// ---------------------------------------------------------------------------
// Storage
// ---------------------------------------------------------------------------

fn default_provider() -> String {
    "microsoft".to_string()
}

#[derive(Serialize, Deserialize, Clone, Default)]
struct AccountData {
    /// "microsoft" | "imap" | "caldav" | "carddav"
    #[serde(default = "default_provider")]
    provider: String,
    // Microsoft OAuth fields
    #[serde(default)]
    access_token: String,
    #[serde(default)]
    refresh_token: Option<String>,
    #[serde(default)]
    expires_at: u64,
    email: String,
    display_name: String,
    #[serde(default)]
    nickname: Option<String>,
    // IMAP fields
    #[serde(default)]
    host: Option<String>,
    #[serde(default)]
    port: Option<u16>,
    #[serde(default)]
    username: Option<String>,
    #[serde(default)]
    password: Option<String>,
    // CalDAV fields
    #[serde(default)]
    caldav_url: Option<String>,
    // CardDAV fields
    #[serde(default)]
    carddav_url: Option<String>,
    // GitHub fields
    #[serde(default)]
    api_base_url: Option<String>,
}

type AccountMap = HashMap<String, AccountData>;

fn accounts_path(app: &tauri::AppHandle) -> PathBuf {
    app.path()
        .app_data_dir()
        .unwrap_or_else(|_| PathBuf::from("."))
        .join("email_accounts.json")
}

/// Load accounts from a specific file path. Missing OR unreadable/corrupt file
/// → empty map (callers treat "no accounts" the same). Secret fields are
/// decrypted (DPAPI) on the way out; legacy plaintext values load unchanged.
/// Pure + testable.
fn load_accounts_at(path: &std::path::Path) -> AccountMap {
    let s = std::fs::read_to_string(path).unwrap_or_default();
    let mut map: AccountMap = serde_json::from_str(&s).unwrap_or_default();
    unprotect_accounts(&mut map);
    map
}

/// Persist accounts to a specific file path (creating the parent dir). Returns
/// an error instead of panicking when serialization or the write fails.
///
/// Atomic: serializes to a unique sibling temp file then renames it over the
/// target, so a crash mid-write can never leave a half-written (corrupt)
/// accounts file: the reader sees either the old file or the new one, never a
/// truncated blob. (N3) Testable.
fn save_accounts_at(path: &std::path::Path, accounts: &AccountMap) -> std::io::Result<()> {
    if let Some(parent) = path.parent() {
        std::fs::create_dir_all(parent)?;
    }
    // Encrypt secret fields (DPAPI) before they touch disk. (S3)
    let json = serde_json::to_string(&protect_accounts(accounts))
        .map_err(|e| std::io::Error::new(std::io::ErrorKind::InvalidData, e))?;
    // Unique temp name (pid + sequence) so two writers never clash on the temp
    // file even though the global store lock already serializes real writers.
    use std::sync::atomic::{AtomicU64, Ordering};
    static TMP_SEQ: AtomicU64 = AtomicU64::new(0);
    let seq = TMP_SEQ.fetch_add(1, Ordering::Relaxed);
    let tmp = path.with_extension(format!("tmp.{}.{}", std::process::id(), seq));
    std::fs::write(&tmp, json)?;
    // rename is atomic on the same volume (Windows MoveFileEx replaces existing).
    match std::fs::rename(&tmp, path) {
        Ok(()) => Ok(()),
        Err(e) => {
            let _ = std::fs::remove_file(&tmp); // don't leak the temp on failure
            Err(e)
        }
    }
}

fn load_accounts(app: &tauri::AppHandle) -> AccountMap {
    load_accounts_at(&accounts_path(app))
}

fn save_accounts(app: &tauri::AppHandle, accounts: &AccountMap) {
    // Best-effort persist. Previously this `.unwrap()`'d the serialize result,
    // so a hiccup would panic while saving credentials. Now it can't panic; a
    // failure is dropped here (the in-memory state is still correct).
    // ignore: best-effort write; next mutation retries.
    let _ = save_accounts_at(&accounts_path(app), accounts);
}

/// Guard for any read-modify-write of the shared accounts file. The store is a
/// single JSON blob holding *all* accounts, so two writers that each do
/// load → mutate-one-entry → save would lose each other's update (e.g. two
/// accounts refreshing at once). Hold this across the whole load/mutate/save so
/// writes serialize. There is no `.await` inside the guarded region, so a plain
/// std Mutex is correct (never held across a suspension point). (N3)
fn account_store_lock() -> std::sync::MutexGuard<'static, ()> {
    static STORE_LOCK: once_cell::sync::Lazy<Mutex<()>> =
        once_cell::sync::Lazy::new(|| Mutex::new(()));
    STORE_LOCK.lock().unwrap_or_else(|e| e.into_inner())
}

/// Per-account async lock so concurrent `get_valid_token` calls for the same
/// account don't each fire a token refresh. For providers that rotate the
/// refresh token on use (Google), parallel refreshes would invalidate all but
/// one, bricking the account. Serializing them means exactly one refresh per
/// expiry window; the rest re-read the freshly-stored token. (N3)
fn refresh_lock_for(account_id: &str) -> std::sync::Arc<tokio::sync::Mutex<()>> {
    static LOCKS: once_cell::sync::Lazy<
        Mutex<HashMap<String, std::sync::Arc<tokio::sync::Mutex<()>>>>,
    > = once_cell::sync::Lazy::new(|| Mutex::new(HashMap::new()));
    let mut map = LOCKS.lock().unwrap_or_else(|e| e.into_inner());
    map.entry(account_id.to_string())
        .or_insert_with(|| std::sync::Arc::new(tokio::sync::Mutex::new(())))
        .clone()
}

// ---------------------------------------------------------------------------
// Secret encryption at rest: Windows DPAPI (S3)
// ---------------------------------------------------------------------------
//
// Tokens and IMAP/CalDAV passwords used to sit in plaintext in
// email_accounts.json. They are now DPAPI-encrypted (per-user, machine-bound)
// at the storage boundary, while non-secret metadata stays readable. Encrypted
// values carry a `dpapi:v1:` tag + base64, so a legacy plaintext file still
// loads (no tag → used as-is) and is re-written encrypted on the next save.

const SECRET_TAG: &str = "dpapi:v1:";

// CRYPTPROTECT_UI_FORBIDDEN is not re-exported by winapi 0.3; the value is stable
// in wincrypt.h. Set so DPAPI never tries to show UI from a background context.
#[cfg(windows)]
const CRYPTPROTECT_UI_FORBIDDEN: u32 = 0x1;

#[cfg(windows)]
struct LocalFreeGuard(*mut u8);
#[cfg(windows)]
impl Drop for LocalFreeGuard {
    fn drop(&mut self) {
        if !self.0.is_null() {
            // SAFETY: pbData was allocated by CryptProtect/UnprotectData, which
            // requires LocalFree. RAII frees it on every return path.
            unsafe {
                winapi::um::winbase::LocalFree(self.0 as *mut _);
            }
        }
    }
}

#[cfg(windows)]
fn dpapi_protect(plaintext: &[u8]) -> Option<Vec<u8>> {
    use std::ptr;
    use winapi::um::dpapi::CryptProtectData;
    use winapi::um::wincrypt::DATA_BLOB;

    let mut in_blob = DATA_BLOB {
        cbData: plaintext.len() as u32,
        pbData: plaintext.as_ptr() as *mut u8,
    };
    let mut out_blob = DATA_BLOB {
        cbData: 0,
        pbData: ptr::null_mut(),
    };
    // SAFETY: in_blob points at a valid slice; out_blob is owned and freed via
    // LocalFreeGuard. All optional params are null per the DPAPI contract.
    let ok = unsafe {
        CryptProtectData(
            &mut in_blob,
            ptr::null(),
            ptr::null_mut(),
            ptr::null_mut(),
            ptr::null_mut(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut out_blob,
        )
    };
    if ok == 0 || out_blob.pbData.is_null() {
        return None;
    }
    let _guard = LocalFreeGuard(out_blob.pbData);
    let bytes =
        unsafe { std::slice::from_raw_parts(out_blob.pbData, out_blob.cbData as usize).to_vec() };
    Some(bytes)
}

#[cfg(windows)]
fn dpapi_unprotect(ciphertext: &[u8]) -> Option<Vec<u8>> {
    use std::ptr;
    use winapi::um::dpapi::CryptUnprotectData;
    use winapi::um::wincrypt::DATA_BLOB;

    let mut in_blob = DATA_BLOB {
        cbData: ciphertext.len() as u32,
        pbData: ciphertext.as_ptr() as *mut u8,
    };
    let mut out_blob = DATA_BLOB {
        cbData: 0,
        pbData: ptr::null_mut(),
    };
    // SAFETY: see dpapi_protect. ppszDataDescr is null (we don't read it back).
    let ok = unsafe {
        CryptUnprotectData(
            &mut in_blob,
            ptr::null_mut(),
            ptr::null_mut(),
            ptr::null_mut(),
            ptr::null_mut(),
            CRYPTPROTECT_UI_FORBIDDEN,
            &mut out_blob,
        )
    };
    if ok == 0 || out_blob.pbData.is_null() {
        return None;
    }
    let _guard = LocalFreeGuard(out_blob.pbData);
    let bytes =
        unsafe { std::slice::from_raw_parts(out_blob.pbData, out_blob.cbData as usize).to_vec() };
    Some(bytes)
}

/// Encrypt a secret for on-disk storage. Empty stays empty (skips the IMAP
/// `access_token` placeholder). On non-Windows (dev only) this is identity.
fn protect_secret(plaintext: &str) -> String {
    if plaintext.is_empty() {
        return String::new();
    }
    #[cfg(windows)]
    {
        use base64::Engine as _;
        match dpapi_protect(plaintext.as_bytes()) {
            Some(ct) => format!(
                "{SECRET_TAG}{}",
                base64::engine::general_purpose::STANDARD.encode(ct)
            ),
            // Encryption failed: keep the plaintext rather than lose the secret;
            // the next save retries. (Rare: DPAPI failing means bigger trouble.)
            None => plaintext.to_string(),
        }
    }
    #[cfg(not(windows))]
    {
        plaintext.to_string()
    }
}

/// Decrypt a stored secret. A `dpapi:v1:` tag → decrypt; anything else is a
/// legacy plaintext value (used as-is, re-encrypted on next save). A tagged but
/// undecryptable value (file moved to another user/machine) → empty string, so
/// the account just needs re-auth instead of crashing.
fn unprotect_secret(stored: &str) -> String {
    match stored.strip_prefix(SECRET_TAG) {
        Some(_tagged) => {
            #[cfg(windows)]
            {
                use base64::Engine as _;
                base64::engine::general_purpose::STANDARD
                    .decode(_tagged)
                    .ok()
                    .and_then(|ct| dpapi_unprotect(&ct))
                    .map(|b| String::from_utf8_lossy(&b).into_owned())
                    .unwrap_or_default()
            }
            #[cfg(not(windows))]
            {
                _tagged.to_string()
            }
        }
        None => stored.to_string(),
    }
}

/// Encrypt every secret field in a map (on a clone) ready for serialization.
fn protect_accounts(accounts: &AccountMap) -> AccountMap {
    accounts
        .iter()
        .map(|(id, v)| {
            let mut d = v.clone();
            d.access_token = protect_secret(&d.access_token);
            d.refresh_token = d.refresh_token.map(|t| protect_secret(&t));
            d.password = d.password.map(|p| protect_secret(&p));
            (id.clone(), d)
        })
        .collect()
}

/// Decrypt every secret field in a freshly-loaded map, in place.
fn unprotect_accounts(accounts: &mut AccountMap) {
    for v in accounts.values_mut() {
        v.access_token = unprotect_secret(&v.access_token);
        v.refresh_token = v.refresh_token.take().map(|t| unprotect_secret(&t));
        v.password = v.password.take().map(|p| unprotect_secret(&p));
    }
}

fn now_secs() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

fn url_host(url: &str) -> String {
    url.trim_start_matches("https://")
        .trim_start_matches("http://")
        .split('/')
        .next()
        .unwrap_or("unknown")
        .to_string()
}

#[cfg(test)]
mod storage_tests {
    use super::*;
    use std::collections::HashMap;

    fn temp_dir(tag: &str) -> std::path::PathBuf {
        std::env::temp_dir().join(format!("themia-email-test-{}-{}", std::process::id(), tag))
    }

    fn sample() -> AccountData {
        AccountData {
            provider: "microsoft".into(),
            email: "user@example.com".into(),
            display_name: "User".into(),
            access_token: "tok".into(),
            refresh_token: Some("refresh".into()),
            expires_at: 12345,
            ..Default::default()
        }
    }

    #[test]
    fn roundtrip_persists_account_fields() {
        let dir = temp_dir("rt");
        let path = dir.join("accounts.json");
        let mut map: AccountMap = HashMap::new();
        map.insert("acc1".into(), sample());
        save_accounts_at(&path, &map).expect("save");

        let loaded = load_accounts_at(&path);
        let got = loaded
            .get("acc1")
            .expect("account present after round-trip");
        assert_eq!(got.email, "user@example.com");
        assert_eq!(got.access_token, "tok");
        assert_eq!(got.refresh_token.as_deref(), Some("refresh"));
        assert_eq!(got.expires_at, 12345);
        let _ = std::fs::remove_dir_all(&dir);
    }

    #[test]
    fn load_missing_file_is_empty() {
        let path = temp_dir("missing").join("nope.json");
        assert!(load_accounts_at(&path).is_empty());
    }

    #[test]
    fn load_corrupt_file_does_not_panic() {
        let dir = temp_dir("corrupt");
        std::fs::create_dir_all(&dir).expect("mkdir");
        let path = dir.join("accounts.json");
        std::fs::write(&path, b"{ this is not valid json").expect("write");
        // Characterizes current behavior: a corrupt file yields an empty map and
        // must never panic (the old load path used unwrap_or_default()).
        assert!(load_accounts_at(&path).is_empty());
        let _ = std::fs::remove_dir_all(&dir);
    }

    // N3: the atomic save (temp + rename) must round-trip AND leave no temp
    // file behind. A leaked temp would mean the rename didn't happen and the
    // target could be stale/half-written.
    #[test]
    fn save_is_atomic_and_leaves_no_temp() {
        let dir = temp_dir("atomic");
        std::fs::create_dir_all(&dir).expect("mkdir");
        let path = dir.join("accounts.json");
        let mut map: AccountMap = HashMap::new();
        map.insert("a".into(), sample());
        save_accounts_at(&path, &map).expect("save");
        save_accounts_at(&path, &map).expect("overwrite save"); // exercise replace-existing

        assert!(load_accounts_at(&path).contains_key("a"));
        let leftovers: Vec<String> = std::fs::read_dir(&dir)
            .expect("read_dir")
            .filter_map(|e| e.ok())
            .map(|e| e.file_name().to_string_lossy().into_owned())
            .filter(|n| n.contains("tmp"))
            .collect();
        assert!(
            leftovers.is_empty(),
            "temp files left behind: {:?}",
            leftovers
        );
        let _ = std::fs::remove_dir_all(&dir);
    }

    // N3: concurrent refreshes for the SAME account must serialize on one lock,
    // while different accounts get independent locks (no false contention).
    #[test]
    fn refresh_lock_is_per_account() {
        let a1 = refresh_lock_for("acc-A");
        let a2 = refresh_lock_for("acc-A");
        let b = refresh_lock_for("acc-B");
        assert!(
            std::sync::Arc::ptr_eq(&a1, &a2),
            "same id must share one lock"
        );
        assert!(
            !std::sync::Arc::ptr_eq(&a1, &b),
            "different ids must not share a lock"
        );
    }

    // S3: a secret survives an encrypt → decrypt round-trip unchanged.
    #[test]
    fn secret_roundtrips_through_protect() {
        let secret = "ya29.super-secret-token-value";
        let stored = protect_secret(secret);
        assert_eq!(unprotect_secret(&stored), secret);
        // Empty stays empty (no crypto on the IMAP access_token placeholder).
        assert_eq!(protect_secret(""), "");
        assert_eq!(unprotect_secret(""), "");
    }

    // S3: a legacy plaintext value (no tag) loads unchanged: the migration path.
    #[test]
    fn legacy_plaintext_secret_loads_unchanged() {
        assert_eq!(
            unprotect_secret("legacy-plaintext-token"),
            "legacy-plaintext-token"
        );
    }

    // S3 (Windows): on disk, no secret appears in plaintext; it still round-trips
    // back to the original on load. Exercises real DPAPI.
    #[cfg(windows)]
    #[test]
    fn secrets_are_encrypted_on_disk() {
        let dir = temp_dir("s3");
        std::fs::create_dir_all(&dir).expect("mkdir");
        let path = dir.join("accounts.json");

        let mut acc = sample();
        acc.access_token = "PLAINTEXT-ACCESS-TOKEN".into();
        acc.refresh_token = Some("PLAINTEXT-REFRESH-TOKEN".into());
        acc.password = Some("PLAINTEXT-PASSWORD".into());
        let mut map: AccountMap = HashMap::new();
        map.insert("a".into(), acc);

        save_accounts_at(&path, &map).expect("save");

        let raw = std::fs::read_to_string(&path).expect("read raw");
        assert!(
            !raw.contains("PLAINTEXT-ACCESS-TOKEN"),
            "access token leaked: {raw}"
        );
        assert!(
            !raw.contains("PLAINTEXT-REFRESH-TOKEN"),
            "refresh token leaked"
        );
        assert!(!raw.contains("PLAINTEXT-PASSWORD"), "password leaked");
        assert!(
            raw.contains(SECRET_TAG),
            "expected encrypted-secret tag on disk"
        );
        // Metadata stays readable.
        assert!(raw.contains("user@example.com"));

        let loaded = load_accounts_at(&path);
        let got = loaded.get("a").expect("account present");
        assert_eq!(got.access_token, "PLAINTEXT-ACCESS-TOKEN");
        assert_eq!(
            got.refresh_token.as_deref(),
            Some("PLAINTEXT-REFRESH-TOKEN")
        );
        assert_eq!(got.password.as_deref(), Some("PLAINTEXT-PASSWORD"));
        let _ = std::fs::remove_dir_all(&dir);
    }
}

#[cfg(test)]
mod pagination_tests {
    use super::*;

    // [S-48] Graph calendar list: parses id/name/color and flags the default
    // calendar (isDefaultCalendar) the frontend uses when no explicit selection
    // is persisted [S-49].
    #[tokio::test]
    async fn graph_calendars_lists_and_flags_default() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1\.0/me/calendars".to_string()))
            .with_status(200)
            .with_body(
                r##"{"value":[
                    {"id":"AAA","name":"Calendar","isDefaultCalendar":true,"hexColor":"#FF0000"},
                    {"id":"BBB","name":"Birthdays","isDefaultCalendar":false,"hexColor":"auto"}
                ]}"##,
            )
            .create_async()
            .await;

        let got = graph_calendars_core(&base, "tok", &http_client())
            .await
            .expect("list should succeed");
        assert_eq!(
            got,
            vec![
                CalendarInfo { id: "AAA".into(), name: "Calendar".into(), is_default: true, color: Some("#ff0000".into()) },
                CalendarInfo { id: "BBB".into(), name: "Birthdays".into(), is_default: false, color: None },
            ]
        );
    }

    // [S-48] Multi-page calendar list must follow @odata.nextLink.
    #[tokio::test]
    async fn graph_calendars_follows_nextlink() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let page2 = format!("{base}/cal-list-2");
        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1\.0/me/calendars".to_string()))
            .with_status(200)
            .with_body(format!(
                r#"{{"value":[{{"id":"A","name":"One","isDefaultCalendar":true}}],"@odata.nextLink":"{page2}"}}"#
            ))
            .create_async()
            .await;
        let _m2 = server
            .mock("GET", "/cal-list-2")
            .with_status(200)
            .with_body(r#"{"value":[{"id":"B","name":"Two","isDefaultCalendar":false}]}"#)
            .create_async()
            .await;

        let got = graph_calendars_core(&base, "tok", &http_client())
            .await
            .expect("pagination should succeed");
        let names: Vec<&str> = got.iter().map(|c| c.name.as_str()).collect();
        assert_eq!(names, vec!["One", "Two"]);
    }

    // [S-48] A 403 from the calendar-list endpoint maps to the same
    // re-consent signal the events path uses.
    #[tokio::test]
    async fn graph_calendars_maps_403_to_not_authorized() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(403)
            .with_body("forbidden")
            .create_async()
            .await;
        let res = graph_calendars_core(&base, "tok", &http_client()).await;
        assert_eq!(res.err().as_deref(), Some("calendar_not_authorized"));
    }

    // [S-48] Google calendarList: `primary` → default, `backgroundColor` → hue,
    // `summary` → name (falling back to id when absent).
    #[tokio::test]
    async fn google_calendars_lists_and_flags_primary() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Regex(r"^/calendar/v3/users/me/calendarList".to_string()))
            .with_status(200)
            .with_body(
                r##"{"items":[
                    {"id":"me@example.com","summary":"Me","primary":true,"backgroundColor":"#0B8043"},
                    {"id":"holidays@group.v.calendar.google.com","backgroundColor":"#FFFFFF"}
                ]}"##,
            )
            .create_async()
            .await;

        let got = google_calendars_core(&base, "tok", &http_client())
            .await
            .expect("list should succeed");
        assert_eq!(
            got,
            vec![
                CalendarInfo { id: "me@example.com".into(), name: "Me".into(), is_default: true, color: Some("#0b8043".into()) },
                CalendarInfo { id: "holidays@group.v.calendar.google.com".into(), name: "holidays@group.v.calendar.google.com".into(), is_default: false, color: Some("#ffffff".into()) },
            ]
        );
    }

    // [S-48] Google calendarList paginates via nextPageToken.
    #[tokio::test]
    async fn google_calendars_follows_pagetoken() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        // Page 1: no pageToken (query ends in maxResults=250).
        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/calendar/v3/users/me/calendarList".to_string()))
            .match_query(mockito::Matcher::Regex(r"maxResults=250$".to_string()))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"a","summary":"A","primary":true}],"nextPageToken":"TK"}"#)
            .create_async()
            .await;
        // Page 2: carries pageToken=TK, no further token.
        let _m2 = server
            .mock("GET", mockito::Matcher::Regex(r"^/calendar/v3/users/me/calendarList".to_string()))
            .match_query(mockito::Matcher::Regex(r"pageToken=TK".to_string()))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"b","summary":"B"}]}"#)
            .create_async()
            .await;

        let got = google_calendars_core(&base, "tok", &http_client())
            .await
            .expect("pagination should succeed");
        let names: Vec<&str> = got.iter().map(|c| c.name.as_str()).collect();
        assert_eq!(names, vec!["A", "B"]);
    }

    // [S-52] Google sign-in must request the calendar-list scope: without it
    // `calendarList` answers 403 and only the primary calendar can be shown.
    #[test]
    fn s52_google_scope_lists_calendars() {
        assert!(GOOGLE_SCOPE.contains("https://www.googleapis.com/auth/calendar.calendarlist.readonly"));
    }

    // [S-51] Color trust boundary: `#rrggbb` kept (lowercased), Apple `#rrggbbaa`
    // truncated to `#rrggbb`, everything else → None.
    #[test]
    fn normalize_cal_color_narrows_to_rrggbb() {
        assert_eq!(normalize_cal_color(Some("#AB12Cd".into())), Some("#ab12cd".into()));
        assert_eq!(normalize_cal_color(Some("#FF2968FF".into())), Some("#ff2968".into()));
        assert_eq!(normalize_cal_color(Some("  #00ff00  ".into())), Some("#00ff00".into()));
        assert_eq!(normalize_cal_color(Some("auto".into())), None);
        assert_eq!(normalize_cal_color(Some(String::new())), None);
        assert_eq!(normalize_cal_color(Some("#12345".into())), None);
        assert_eq!(normalize_cal_color(Some("#gggggg".into())), None);
        assert_eq!(normalize_cal_color(None), None);
    }

    // N1 regression: a >1-page Graph response must be fully collected via
    // @odata.nextLink. Pre-fix this returned only page 1 ("Alice").
    #[tokio::test]
    async fn graph_contact_birthdays_follows_nextlink_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let page2 = format!("{base}/contacts-page-2");

        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1\.0/me/contacts".to_string()))
            .with_status(200)
            .with_body(format!(
                r#"{{"value":[{{"displayName":"Alice","birthday":"1990-05-10T00:00:00Z"}}],"@odata.nextLink":"{page2}"}}"#
            ))
            .create_async()
            .await;
        let _m2 = server
            .mock("GET", "/contacts-page-2")
            .with_status(200)
            .with_body(r#"{"value":[{"displayName":"Bob","birthday":"1985-12-01T00:00:00Z"}]}"#)
            .create_async()
            .await;

        let client = http_client();
        let got = graph_contact_birthdays_core(&base, "tok", &client)
            .await
            .expect("pagination should succeed");

        let names: Vec<&str> = got.iter().map(|b| b.display_name.as_str()).collect();
        assert_eq!(names, vec!["Alice", "Bob"]);
    }

    #[tokio::test]
    async fn graph_contact_birthdays_single_page_terminates() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(200)
            .with_body(r#"{"value":[{"displayName":"Solo","birthday":"2000-01-02T00:00:00Z"}]}"#)
            .create_async()
            .await;

        let got = graph_contact_birthdays_core(&base, "tok", &reqwest::Client::new())
            .await
            .expect("single page ok");
        assert_eq!(got.len(), 1);
        assert_eq!((got[0].month, got[0].day), (1, 2));
    }

    // N1 regression: a >1-page Graph calendarView response must be fully
    // collected via @odata.nextLink. Pre-fix this returned only page 1.
    #[tokio::test]
    async fn graph_calendar_follows_nextlink_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let page2 = format!("{base}/cal-page-2");

        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1\.0/me/calendarView".to_string()))
            .with_status(200)
            .with_body(format!(
                r#"{{"value":[{{"id":"a","subject":"One","isAllDay":false}}],"@odata.nextLink":"{page2}"}}"#
            ))
            .create_async()
            .await;
        let _m2 = server
            .mock("GET", "/cal-page-2")
            .with_status(200)
            .with_body(r#"{"value":[{"id":"b","subject":"Two","isAllDay":false}]}"#)
            .create_async()
            .await;

        let client = http_client();
        let got = graph_calendar_core(
            &base,
            "tok",
            &client,
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            None,
        )
        .await
        .expect("pagination should succeed");

        let subjects: Vec<&str> = got.iter().map(|e| e.subject.as_str()).collect();
        assert_eq!(subjects, vec!["One", "Two"]);
    }

    // N1: a 403 from Graph calendar must surface as `calendar_not_authorized`
    // (the UI maps this to a re-consent prompt), not a generic API error.
    #[tokio::test]
    async fn graph_calendar_maps_403_to_not_authorized() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(403)
            .with_body("forbidden")
            .create_async()
            .await;

        let res = graph_calendar_core(
            &base,
            "tok",
            &reqwest::Client::new(),
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            None,
        )
        .await;
        assert_eq!(res.err().as_deref(), Some("calendar_not_authorized"));
    }

    // 429 regression: a Graph throttle (429 + Retry-After) on the first call
    // must be retried, honoring Retry-After, and succeed, instead of surfacing
    // "Graph API error 429" that the user had to manually reload to clear.
    // Pre-fix graph_calendar_core made a single attempt and returned the 429.
    #[tokio::test]
    async fn graph_calendar_retries_on_429_then_succeeds() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();

        // First hit: throttled. Retry-After: 0 keeps the test instant while
        // still exercising the header-parse + retry path.
        let _m429 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/calendarView".to_string()),
            )
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .expect(1)
            .create_async()
            .await;
        // Second hit (the retry): success.
        let _m200 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/calendarView".to_string()),
            )
            .with_status(200)
            .with_body(r#"{"value":[{"id":"a","subject":"Recovered","isAllDay":false}]}"#)
            .expect(1)
            .create_async()
            .await;

        let got = graph_calendar_core(
            &base,
            "tok",
            &http_client(),
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            None,
        )
        .await
        .expect("429 then 200 should resolve to the 200 payload");

        let subjects: Vec<&str> = got.iter().map(|e| e.subject.as_str()).collect();
        assert_eq!(subjects, vec!["Recovered"]);
        _m429.assert_async().await;
        _m200.assert_async().await;
    }

    // 429: a throttle that never clears (every attempt 429) must eventually give
    // up and surface "Graph API error 429" rather than retrying forever.
    #[tokio::test]
    async fn graph_calendar_persistent_429_surfaces_error() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .create_async()
            .await;

        let res = graph_calendar_core(
            &base,
            "tok",
            &http_client(),
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            None,
        )
        .await;
        assert_eq!(res.err().as_deref(), Some("Graph API error 429"));
    }

    // Retry-After parsing: delta-seconds form parses; absent/garbage is None.
    #[tokio::test]
    async fn retry_after_delay_parses_delta_seconds() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(429)
            .with_header("Retry-After", "5")
            .with_body("x")
            .create_async()
            .await;
        let resp = http_client().get(&base).send().await.expect("req");
        assert_eq!(
            retry_after_delay(&resp),
            Some(std::time::Duration::from_secs(5))
        );

        let mut server2 = mockito::Server::new_async().await;
        let base2 = server2.url();
        let _m2 = server2
            .mock("GET", mockito::Matcher::Any)
            .with_status(429)
            .with_body("x")
            .create_async()
            .await;
        let resp2 = http_client().get(&base2).send().await.expect("req");
        assert_eq!(retry_after_delay(&resp2), None);
    }

    // N1 regression: a >1-page Graph To Do task list must be fully collected via
    // @odata.nextLink. Pre-fix this returned only page 1.
    #[tokio::test]
    async fn graph_todo_tasks_follows_nextlink_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let page2 = format!("{base}/tasks-page-2");

        let _m1 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L1/tasks".to_string()),
            )
            .with_status(200)
            .with_body(format!(
                r#"{{"value":[{{"id":"a","title":"One"}}],"@odata.nextLink":"{page2}"}}"#
            ))
            .create_async()
            .await;
        let _m2 = server
            .mock("GET", "/tasks-page-2")
            .with_status(200)
            .with_body(r#"{"value":[{"id":"b","title":"Two"}]}"#)
            .create_async()
            .await;

        let got = graph_todo_tasks_core(&base, "tok", &reqwest::Client::new(), "L1")
            .await
            .expect("pagination should succeed");
        let titles: Vec<&str> = got.iter().map(|t| t.title.as_str()).collect();
        assert_eq!(titles, vec!["One", "Two"]);
    }

    // Issue #101 contract: Graph hands us `dueDateTime` as a wall clock plus a
    // separate `timeZone`, and the To Do mobile app writes the creator's local
    // midnight already converted to UTC. We forward the `dateTime` verbatim —
    // zoneless, 7-digit fraction, whatever hour it carries — because the
    // frontend ([S-48], todo/dueDate.ts) is what resolves it to a calendar day.
    // Pinned so nobody "helpfully" reformats or truncates it here.
    #[tokio::test]
    async fn graph_todo_task_due_date_passes_through_verbatim() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L1/tasks".to_string()),
            )
            .with_status(200)
            .with_body(
                r#"{"value":[
                    {"id":"a","title":"Mobile","status":"notStarted",
                     "dueDateTime":{"dateTime":"2026-08-09T17:00:00.0000000","timeZone":"UTC"}},
                    {"id":"b","title":"Midnight","status":"notStarted",
                     "dueDateTime":{"dateTime":"2026-08-10T00:00:00.0000000","timeZone":"UTC"}},
                    {"id":"c","title":"No due date","status":"notStarted"}
                ]}"#,
            )
            .create_async()
            .await;

        let got = graph_todo_tasks_core(&base, "tok", &reqwest::Client::new(), "L1")
            .await
            .expect("fetch should succeed");
        let dues: Vec<Option<&str>> = got.iter().map(|t| t.due_date.as_deref()).collect();
        assert_eq!(
            dues,
            vec![
                Some("2026-08-09T17:00:00.0000000"),
                Some("2026-08-10T00:00:00.0000000"),
                None,
            ]
        );
    }

    // [S-52] Today has no list of its own on either backend, so it is the union
    // of every list's tasks — with each task still carrying the list it came
    // from, which is what the widget's writes are addressed to.
    #[tokio::test]
    async fn graph_todo_today_fans_out_across_lists() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _lists = server
            .mock("GET", "/v1.0/me/todo/lists")
            .with_status(200)
            .with_body(
                r#"{"value":[
                    {"id":"L1","displayName":"Tasks","wellknownListName":"defaultList"},
                    {"id":"L2","displayName":"Work"}
                ]}"#,
            )
            .create_async()
            .await;
        let _t1 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L1/tasks".to_string()),
            )
            .with_status(200)
            .with_body(r#"{"value":[{"id":"a","title":"One"}]}"#)
            .create_async()
            .await;
        let _t2 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L2/tasks".to_string()),
            )
            .with_status(200)
            .with_body(r#"{"value":[{"id":"b","title":"Two"}]}"#)
            .create_async()
            .await;

        let client = reqwest::Client::new();
        let lists = graph_todo_lists_core(&base, "tok", &client)
            .await
            .expect("lists should load");
        let base_owned = base.clone();
        let got = todo_today_core(lists, move |id| {
            let (base, client) = (base_owned.clone(), client.clone());
            Box::pin(async move { graph_todo_tasks_core(&base, "tok", &client, &id).await })
        })
        .await
        .expect("fan-out should succeed");

        let rows: Vec<(&str, &str)> = got
            .iter()
            .map(|t| (t.title.as_str(), t.list_id.as_str()))
            .collect();
        assert_eq!(rows, vec![("One", "L1"), ("Two", "L2")]);
    }

    // [S-63] Regression, issue nwalser/themia-releases#113: To Do was the one
    // Graph caller left off the 429 backoff, so a throttle it provoked itself
    // (Today reads EVERY list, [S-52]) surfaced as "Graph API error 429" in the
    // widget. The lists call must honor Retry-After and retry.
    #[tokio::test]
    async fn s63_todo_lists_retries_on_429_then_succeeds() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        // Retry-After: 0 keeps the test instant while still exercising the
        // header-parse + retry path.
        let _m429 = server
            .mock("GET", "/v1.0/me/todo/lists")
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .expect(1)
            .create_async()
            .await;
        let _m200 = server
            .mock("GET", "/v1.0/me/todo/lists")
            .with_status(200)
            .with_body(r#"{"value":[{"id":"L1","displayName":"Tasks"}]}"#)
            .expect(1)
            .create_async()
            .await;

        let got = graph_todo_lists_core(&base, "tok", &reqwest::Client::new())
            .await
            .expect("429 then 200 should resolve to the 200 payload");
        assert_eq!(got.len(), 1);
        assert_eq!(got[0].display_name, "Tasks");
        _m429.assert_async().await;
        _m200.assert_async().await;
    }

    // [S-63] Same for the per-list task read — the call the Today fan-out makes
    // once per list, and so the one that actually trips the throttle.
    #[tokio::test]
    async fn s63_todo_tasks_retries_on_429_then_succeeds() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m429 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L1/tasks".to_string()),
            )
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .expect(1)
            .create_async()
            .await;
        let _m200 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/v1\.0/me/todo/lists/L1/tasks".to_string()),
            )
            .with_status(200)
            .with_body(r#"{"value":[{"id":"a","title":"Recovered"}]}"#)
            .expect(1)
            .create_async()
            .await;

        let got = graph_todo_tasks_core(&base, "tok", &reqwest::Client::new(), "L1")
            .await
            .expect("429 then 200 should resolve to the 200 payload");
        let titles: Vec<&str> = got.iter().map(|t| t.title.as_str()).collect();
        assert_eq!(titles, vec!["Recovered"]);
        _m429.assert_async().await;
        _m200.assert_async().await;
    }

    // [S-63] Google Tasks throttles the same way and shares the same helper.
    #[tokio::test]
    async fn s63_google_todo_lists_retries_on_429_then_succeeds() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m429 = server
            .mock("GET", "/tasks/v1/users/@me/lists")
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .expect(1)
            .create_async()
            .await;
        let _m200 = server
            .mock("GET", "/tasks/v1/users/@me/lists")
            .with_status(200)
            .with_body(r#"{"items":[{"id":"L1","title":"My Tasks"}]}"#)
            .expect(1)
            .create_async()
            .await;

        let got = google_todo_lists_core(&base, "tok", &reqwest::Client::new())
            .await
            .expect("429 then 200 should resolve to the 200 payload");
        assert_eq!(got.len(), 1);
        assert_eq!(got[0].display_name, "My Tasks");
        _m429.assert_async().await;
        _m200.assert_async().await;
    }

    // [S-63] A throttle that never clears still gives up rather than retrying
    // forever — the widget says 429, it doesn't hang.
    #[tokio::test]
    async fn s63_todo_persistent_429_surfaces_error() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("GET", mockito::Matcher::Any)
            .with_status(429)
            .with_header("Retry-After", "0")
            .with_body("throttled")
            .create_async()
            .await;

        let res = graph_todo_lists_core(&base, "tok", &reqwest::Client::new()).await;
        assert_eq!(res.err().as_deref(), Some("Graph API error 429"));
    }

    // [S-63] Regression, issue nwalser/themia-releases#113: a transport failure
    // used to reach the widget as reqwest's own "error sending request for url
    // (https://graph.microsoft.com/v1.0/me/todo/lists)" — untranslated, and
    // naming the URL rather than the cause. It must come out as the sentinel the
    // frontend localizes.
    #[tokio::test]
    async fn s63_todo_transport_failure_is_a_localizable_sentinel() {
        // A port nothing is listening on: bind one, learn its number, drop it.
        let addr = {
            let l = std::net::TcpListener::bind("127.0.0.1:0").expect("bind");
            l.local_addr().expect("addr")
        };
        let base = format!("http://{addr}");

        let res = graph_todo_lists_core(&base, "tok", &reqwest::Client::new()).await;
        assert_eq!(res.err().as_deref(), Some("network_error"));

        let res = graph_todo_tasks_core(&base, "tok", &reqwest::Client::new(), "L1").await;
        assert_eq!(res.err().as_deref(), Some("network_error"));
    }

    // [S-52] One unreachable list (shared list that lost access, transient 500)
    // must not blank the whole Today view.
    #[tokio::test]
    async fn todo_today_skips_a_failing_list() {
        let lists = vec![
            TodoTaskList {
                id: "ok".into(),
                display_name: "Tasks".into(),
                is_default: true,
            },
            TodoTaskList {
                id: "broken".into(),
                display_name: "Shared".into(),
                is_default: false,
            },
        ];
        let got = todo_today_core(lists, |id| {
            Box::pin(async move {
                if id == "broken" {
                    Err("Graph API error 500".to_string())
                } else {
                    Ok(vec![TodoTask {
                        id: "a".into(),
                        title: "One".into(),
                        status: "notStarted".into(),
                        importance: "normal".into(),
                        due_date: None,
                        completed_at: None,
                        list_id: id,
                    }])
                }
            })
        })
        .await
        .expect("a single failing list should not fail the view");
        assert_eq!(got.len(), 1);
        assert_eq!(got[0].list_id, "ok");
    }

    // [S-52] …but if EVERY list fails, the error is surfaced. An empty Ok here
    // would render as "nothing due today", which is a lie the user can't see
    // through.
    #[tokio::test]
    async fn todo_today_surfaces_error_when_every_list_fails() {
        let lists = vec![TodoTaskList {
            id: "broken".into(),
            display_name: "Tasks".into(),
            is_default: true,
        }];
        let err = todo_today_core(lists, |_| {
            Box::pin(async move { Err("todo_not_authorized".to_string()) })
        })
        .await
        .expect_err("an all-failing fan-out must not look empty");
        assert_eq!(err, "todo_not_authorized");
    }

    // [S-52] The due day sent on create is interpolated into a request body, so
    // only a bare `YYYY-MM-DD` is accepted; anything else creates an undated
    // task rather than travelling into the JSON.
    #[test]
    fn valid_due_day_accepts_only_a_bare_date() {
        assert_eq!(valid_due_day(Some("2026-08-10")), Some("2026-08-10"));
        assert_eq!(valid_due_day(None), None);
        assert_eq!(valid_due_day(Some("")), None);
        assert_eq!(valid_due_day(Some("2026-08-10T00:00:00Z")), None);
        assert_eq!(valid_due_day(Some("10.08.2026")), None);
        assert_eq!(valid_due_day(Some(r#"2026-08-1","x":"#)), None);
    }

    // N1 regression: a >1-page Google Tasks list must be fully collected via
    // nextPageToken. Pre-fix this returned only page 1.
    #[tokio::test]
    async fn google_todo_tasks_follows_pagetoken_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();

        // Page 1: query has no pageToken (anchored regex excludes it).
        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/tasks/v1/lists/L1/tasks".to_string()))
            .match_query(mockito::Matcher::Regex(
                r"^maxResults=100&showCompleted=true&showHidden=false$".to_string(),
            ))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"a","title":"One","status":"needsAction"}],"nextPageToken":"TK"}"#)
            .create_async()
            .await;
        // Page 2: pageToken=TK present.
        let _m2 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/tasks/v1/lists/L1/tasks".to_string()),
            )
            .match_query(mockito::Matcher::Regex(r"pageToken=TK".to_string()))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"b","title":"Two","status":"completed"}]}"#)
            .create_async()
            .await;

        let got = google_todo_tasks_core(&base, "tok", &reqwest::Client::new(), "L1", true)
            .await
            .expect("pagination should succeed");
        let titles: Vec<&str> = got.iter().map(|t| t.title.as_str()).collect();
        assert_eq!(titles, vec!["One", "Two"]);
        // Status mapping: Google "completed" → MS "completed".
        assert_eq!(got[1].status, "completed");
    }

    // N1 regression: a >1-page Google Calendar response must be fully collected
    // via nextPageToken. Pre-fix this returned only page 1.
    #[tokio::test]
    async fn google_calendar_follows_pagetoken_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();

        let _m1 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/calendar/v3/calendars/primary/events".to_string()),
            )
            .match_query(mockito::Matcher::Regex(r"maxResults=100$".to_string()))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"a","summary":"One"}],"nextPageToken":"TK"}"#)
            .create_async()
            .await;
        let _m2 = server
            .mock(
                "GET",
                mockito::Matcher::Regex(r"^/calendar/v3/calendars/primary/events".to_string()),
            )
            .match_query(mockito::Matcher::Regex(r"pageToken=TK".to_string()))
            .with_status(200)
            .with_body(r#"{"items":[{"id":"b","summary":"Two"}]}"#)
            .create_async()
            .await;

        let got = google_calendar_core(
            &base,
            "tok",
            &reqwest::Client::new(),
            "2024-01-01T00:00:00Z",
            "2024-12-31T00:00:00Z",
            None,
        )
        .await
        .expect("pagination should succeed");
        let subjects: Vec<&str> = got.iter().map(|e| e.subject.as_str()).collect();
        assert_eq!(subjects, vec!["One", "Two"]);
    }

    // N1 regression: a >1-page Google People birthdays response must be fully
    // collected via nextPageToken. Pre-fix this returned only page 1.
    #[tokio::test]
    async fn google_people_birthdays_follows_pagetoken_across_pages() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();

        let _m1 = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1/people/me/connections".to_string()))
            .match_query(mockito::Matcher::Regex(r"pageSize=500$".to_string()))
            .with_status(200)
            .with_body(r#"{"connections":[{"names":[{"displayName":"Alice"}],"birthdays":[{"date":{"month":5,"day":10,"year":1990}}]}],"nextPageToken":"TK"}"#)
            .create_async()
            .await;
        let _m2 = server
            .mock("GET", mockito::Matcher::Regex(r"^/v1/people/me/connections".to_string()))
            .match_query(mockito::Matcher::Regex(r"pageToken=TK".to_string()))
            .with_status(200)
            .with_body(r#"{"connections":[{"names":[{"displayName":"Bob"}],"birthdays":[{"date":{"month":12,"day":1}}]}]}"#)
            .create_async()
            .await;

        let got = google_people_birthdays_core(&base, "tok", &reqwest::Client::new())
            .await
            .expect("pagination should succeed");
        let names: Vec<&str> = got.iter().map(|b| b.display_name.as_str()).collect();
        assert_eq!(names, vec!["Alice", "Bob"]);
    }
}

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

#[derive(Serialize, Deserialize, Clone)]
pub struct AccountInfo {
    pub id: String,
    pub email: String,
    pub display_name: String,
    pub nickname: Option<String>,
    pub provider: String,
}

#[derive(Serialize, Deserialize)]
pub struct DeviceAuthStart {
    pub user_code: String,
    pub verification_url: String,
    pub device_code: String,
    pub interval: u64,
}

#[derive(Serialize, Deserialize, Clone)]
pub struct EmailMessage {
    pub id: String,
    pub subject: String,
    pub from_name: String,
    pub from_email: String,
    pub received_at: String,
    pub is_read: bool,
    pub preview: String,
    pub web_link: String,
    pub account_id: String,
    pub account_email: String,
    pub account_nickname: String,
    pub account_provider: String,
}

// ---------------------------------------------------------------------------
// Account management
// ---------------------------------------------------------------------------

pub fn list_accounts(app: &tauri::AppHandle) -> Vec<AccountInfo> {
    load_accounts(app)
        .into_iter()
        .map(|(id, data)| AccountInfo {
            id,
            email: data.email,
            display_name: data.display_name,
            nickname: data.nickname,
            provider: data.provider,
        })
        .collect()
}

pub fn sign_out_account(app: &tauri::AppHandle, account_id: &str) {
    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.remove(account_id);
    save_accounts(app, &accounts);
}

pub fn rename_account(app: &tauri::AppHandle, account_id: &str, nickname: String) {
    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    if let Some(acc) = accounts.get_mut(account_id) {
        let trimmed = nickname.trim().to_string();
        acc.nickname = if trimmed.is_empty() {
            None
        } else {
            Some(trimmed)
        };
    }
    save_accounts(app, &accounts);
}

// ---------------------------------------------------------------------------
// Add IMAP account
// ---------------------------------------------------------------------------

pub async fn add_imap_account(
    app: &tauri::AppHandle,
    host: String,
    port: u16,
    username: String,
    password: String,
    display_name: String,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    // Test credentials by actually connecting
    let host_c = host.clone();
    let username_c = username.clone();
    let password_c = password.clone();

    tokio::task::spawn_blocking(move || -> Result<(), String> {
        let client = imap_connect(&host_c, port)?;
        let mut session = client
            .login(&username_c, &password_c)
            .map_err(|(e, _)| format!("Login failed: {}", e))?;
        session.logout().ok();
        Ok(())
    })
    .await
    .map_err(|e| e.to_string())??;

    let email = if username.contains('@') {
        username.clone()
    } else {
        format!("{}@{}", username, host)
    };
    let id = format!("imap:{}@{}", username, host);

    let data = AccountData {
        provider: "imap".to_string(),
        access_token: String::new(),
        refresh_token: None,
        expires_at: 0,
        email: email.clone(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: Some(host),
        port: Some(port),
        username: Some(username),
        password: Some(password),
        caldav_url: None,
        carddav_url: None,
        api_base_url: None,
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email,
        display_name,
        nickname,
        provider: "imap".to_string(),
    })
}

// ---------------------------------------------------------------------------
// Add CalDAV account
// ---------------------------------------------------------------------------

pub async fn add_caldav_account(
    app: &tauri::AppHandle,
    url: String,
    username: String,
    password: String,
    display_name: String,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    // Resolve the typed URL to an actual calendar collection (RFC 6764
    // discovery), verifying credentials in the process. An exact collection URL
    // is used verbatim; a base/server URL is walked to a calendar. [S-45]
    let resolved_url =
        dav_discover_collection(&dav_client(), &url, &username, &password, DavKind::Cal).await?;

    let email = username.clone();
    let host = url_host(&resolved_url);
    let id = format!("caldav:{}@{}", username, host);

    let data = AccountData {
        provider: "caldav".to_string(),
        access_token: String::new(),
        refresh_token: None,
        expires_at: 0,
        email: email.clone(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: None,
        port: None,
        username: Some(username),
        password: Some(password),
        caldav_url: Some(resolved_url),
        carddav_url: None,
        api_base_url: None,
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email,
        display_name,
        nickname,
        provider: "caldav".to_string(),
    })
}

// ---------------------------------------------------------------------------
// Add CardDAV account
// ---------------------------------------------------------------------------

pub async fn add_carddav_account(
    app: &tauri::AppHandle,
    url: String,
    username: String,
    password: String,
    display_name: String,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    // Resolve the typed URL to an actual address-book collection (RFC 6764
    // discovery), verifying credentials in the process. An exact collection URL
    // is used verbatim; a base/server URL is walked to an address book. [S-45]
    let resolved_url =
        dav_discover_collection(&dav_client(), &url, &username, &password, DavKind::Card).await?;

    let email = username.clone();
    let host = url_host(&resolved_url);
    let id = format!("carddav:{}@{}", username, host);

    let data = AccountData {
        provider: "carddav".to_string(),
        access_token: String::new(),
        refresh_token: None,
        expires_at: 0,
        email: email.clone(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: None,
        port: None,
        username: Some(username),
        password: Some(password),
        caldav_url: None,
        carddav_url: Some(resolved_url),
        api_base_url: None,
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email,
        display_name,
        nickname,
        provider: "carddav".to_string(),
    })
}

// ---------------------------------------------------------------------------
// Add GitHub account (personal access token)
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct GitHubUser {
    login: String,
    #[serde(default)]
    name: Option<String>,
    #[serde(default)]
    email: Option<String>,
}

pub async fn add_github_account(
    app: &tauri::AppHandle,
    token: String,
    api_base_url: Option<String>,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    persist_github_token(app, token, api_base_url, nickname).await
}

/// Verify a GitHub token against `/user` and store the account. Shared by both
/// ways in — the personal-access-token form and the OAuth device flow ([S-51])
/// — so the account id, display-name fallbacks, and the "is this token any
/// good?" check exist exactly once.
async fn persist_github_token(
    app: &tauri::AppHandle,
    token: String,
    api_base_url: Option<String>,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    let api = api_base_url
        .as_deref()
        .map(|s| s.trim_end_matches('/').to_string())
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| "https://api.github.com".to_string());

    let client = http_client();

    let resp = client
        .get(format!("{}/user", api))
        .header("Accept", "application/vnd.github+json")
        .header("User-Agent", "themia-app")
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| format!("Cannot reach {}: {}", api, e))?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(format!(
            "GitHub returned HTTP {}, check token scopes (needs repo)",
            s
        ));
    }

    let me: GitHubUser = resp
        .json()
        .await
        .map_err(|e| format!("Bad /user response: {}", e))?;
    let display_name = me.name.unwrap_or_else(|| me.login.clone());
    let email = me
        .email
        .unwrap_or_else(|| format!("{}@users.noreply.github.com", me.login));
    let host = url_host(&api);
    let id = format!("github:{}@{}", me.login, host);

    let data = AccountData {
        provider: "github".to_string(),
        access_token: token,
        refresh_token: None,
        expires_at: 0,
        email: email.clone(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: None,
        port: None,
        username: Some(me.login),
        password: None,
        caldav_url: None,
        carddav_url: None,
        api_base_url: Some(api),
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email,
        display_name,
        nickname,
        provider: "github".to_string(),
    })
}

// ---------------------------------------------------------------------------
// GitHub sign-in (OAuth device flow) — [S-51]
//
// Same shape as the Microsoft device flow above, and for the same reason: a
// desktop app has nowhere safe to keep a client secret, and GitHub's device
// flow is the grant designed for exactly that — `client_id` only, the user
// approves in a browser, the app polls.
//
// This does NOT replace `add_github_account` (the personal-access-token path).
// GitHub Enterprise Server can't authenticate against a github.com OAuth app,
// so a token stays the only way in for those users. Both paths converge on the
// same `persist_github_token`, so there is one place that verifies a token and
// writes the account, not two.
// ---------------------------------------------------------------------------

/// Client id of the "Themia" GitHub OAuth App (github.com/settings/applications/3783370),
/// which has **Enable Device Flow** ticked. Public by design — the device flow
/// ships no client secret, which is exactly why it suits a desktop app; no
/// secret was ever generated for this app.
const GITHUB_CLIENT_ID: &str = "Ov23liel8RYOXTaNaVI6";

/// Matches the scopes the token form asks for, so both paths grant the same
/// access and no widget works on one but not the other.
const GITHUB_SCOPE: &str = "repo read:user notifications";

fn github_client_id() -> Result<&'static str, String> {
    if GITHUB_CLIENT_ID == "REPLACE_WITH_GITHUB_OAUTH_APP_CLIENT_ID" {
        return Err(
            "GitHub sign-in is not configured yet. Register an OAuth App at \
             github.com/settings/applications/new with Device Flow enabled, then set \
             GITHUB_CLIENT_ID in widgets/office/email/src/lib.rs. You can connect with a \
             personal access token in the meantime."
                .into(),
        );
    }
    Ok(GITHUB_CLIENT_ID)
}

#[derive(Deserialize)]
struct GitHubDeviceCode {
    device_code: String,
    user_code: String,
    verification_uri: String,
    interval: u64,
}

#[derive(Deserialize)]
struct GitHubDeviceToken {
    access_token: Option<String>,
    error: Option<String>,
    #[serde(default)]
    error_description: Option<String>,
}

/// Request a device + user code. The caller shows the code, opens the
/// verification URL, and then polls `poll_github_device_auth`.
pub async fn start_github_device_auth() -> Result<DeviceAuthStart, String> {
    let client_id = github_client_id()?;
    let resp = http_client()
        .post("https://github.com/login/device/code")
        .header("Accept", "application/json")
        .header("User-Agent", "themia-app")
        .form(&[("client_id", client_id), ("scope", GITHUB_SCOPE)])
        .send()
        .await
        .map_err(|e| format!("Cannot reach github.com: {}", e))?;

    if !resp.status().is_success() {
        let status = resp.status().as_u16();
        let body = resp.text().await.unwrap_or_default();
        return Err(format!("Device code request failed (HTTP {}): {}", status, body));
    }

    let dc: GitHubDeviceCode = resp
        .json()
        .await
        .map_err(|e| format!("Bad device-code response: {}", e))?;

    Ok(DeviceAuthStart {
        user_code: dc.user_code,
        verification_url: dc.verification_uri,
        device_code: dc.device_code,
        // GitHub rejects polling faster than `interval` with `slow_down`;
        // honor whatever it asks for rather than assuming 5s.
        interval: dc.interval,
    })
}

/// One poll. `Ok(None)` means "still waiting" — the user hasn't approved yet,
/// which is the normal state for most of this flow, not an error.
pub async fn poll_github_device_auth(
    app: &tauri::AppHandle,
    device_code: &str,
) -> Result<Option<AccountInfo>, String> {
    let client_id = github_client_id()?;
    let resp = http_client()
        .post("https://github.com/login/oauth/access_token")
        .header("Accept", "application/json")
        .header("User-Agent", "themia-app")
        .form(&[
            ("client_id", client_id),
            ("device_code", device_code),
            ("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
        ])
        .send()
        .await
        .map_err(|e| format!("Cannot reach github.com: {}", e))?;

    let tr: GitHubDeviceToken = resp
        .json()
        .await
        .map_err(|e| format!("Bad token response: {}", e))?;

    if let Some(err) = tr.error.as_deref() {
        return match err {
            // Expected while the user is still on the approval page.
            "authorization_pending" | "slow_down" => Ok(None),
            "expired_token" => Err("The code expired. Start the connection again.".into()),
            "access_denied" => Err("Access was denied on GitHub. Approve the request to connect.".into()),
            other => Err(format!(
                "GitHub sign-in failed: {}",
                tr.error_description.as_deref().unwrap_or(other)
            )),
        };
    }

    let token = tr.access_token.ok_or("GitHub returned no access token")?;
    // github.com only: an Enterprise host needs its own OAuth app, so the
    // device flow never targets one. Enterprise users take the token path.
    persist_github_token(app, token, None, None).await.map(Some)
}

pub fn get_github_token(app: &tauri::AppHandle, account_id: &str) -> Option<String> {
    load_accounts(app)
        .get(account_id)
        .filter(|a| a.provider == "github")
        .map(|a| a.access_token.clone())
}

pub fn get_github_api_base_url(app: &tauri::AppHandle, account_id: &str) -> Option<String> {
    load_accounts(app)
        .get(account_id)
        .filter(|a| a.provider == "github")
        .and_then(|a| a.api_base_url.clone())
}

// ---------------------------------------------------------------------------
// Claude Code usage  [S-01..S-08]
//
// Themia runs its OWN OAuth (authorization-code + PKCE) against Anthropic and
// stores the resulting access/refresh token in the shared account store as
// provider "claude". Crucially it requests the `user:profile` scope: the usage
// endpoint REQUIRES it, and a `claude setup-token` token (which carries only
// `user:inference`) is rejected with 403 `does not meet scope requirement
// user:profile` (issue #72). Themia never reads Claude Code's own
// ~/.claude/.credentials.json: that file is single-writer-owned by the running
// CLI, and fighting it over refresh-token rotation would brick auth. Tokens are
// DPAPI-encrypted at rest. The OAuth endpoints are undocumented (they mirror the
// CLI's own subscription login) and may change without notice. [S-08]
// ---------------------------------------------------------------------------

const CLAUDE_USAGE_URL: &str = "https://api.anthropic.com/api/oauth/usage";
const CLAUDE_OAUTH_BETA: &str = "oauth-2025-04-20";
const PROVIDER_CLAUDE: &str = "claude";

// OAuth client Themia reuses to mirror the CLI's subscription login. The
// authorize page returns `code#state` on a hosted callback the user copies back
// (no loopback redirect is whitelisted for this client), and the token endpoint
// takes form-urlencoded bodies.
const CLAUDE_CLIENT_ID: &str = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";
const CLAUDE_AUTHORIZE_URL: &str = "https://claude.ai/oauth/authorize";
const CLAUDE_TOKEN_URL: &str = "https://platform.claude.com/v1/oauth/token";
const CLAUDE_REDIRECT_URI: &str = "https://platform.claude.com/oauth/code/callback";
// `user:profile` is the scope the usage endpoint demands (#72); `user:inference`
// keeps parity with the CLI's grant. Request the minimum needed.
const CLAUDE_SCOPES: &str = "user:profile user:inference";
// The usage endpoint routes any non-`claude-code/<version>` User-Agent into an
// aggressively rate-limited bucket (persistent 429s). Mirror the CLI's UA.
const CLAUDE_USER_AGENT: &str = "claude-code/2.1.139";

/// One rate-limit window from the usage endpoint. `utilization` is how much of
/// the window's limit is consumed (the endpoint's scale is normalized to a
/// percentage in the UI, see claudeUsage.ts); `resets_at` is an ISO-8601
/// timestamp, `null` until usage accrues in the window. [S-06]
#[derive(Serialize, Deserialize, Clone, Default, Debug, PartialEq)]
pub struct UsageWindow {
    #[serde(default)]
    pub utilization: f64,
    #[serde(default)]
    pub resets_at: Option<String>,
}

/// The subset of the usage payload Themia renders. Unknown/experimental buckets
/// in the response (codename keys, `extra_usage`, and so on) are ignored: serde
/// drops unknown fields, so a server-side addition can't break parsing. [S-07]
#[derive(Serialize, Deserialize, Clone, Default, Debug, PartialEq)]
pub struct ClaudeUsage {
    #[serde(default)]
    pub five_hour: UsageWindow,
    #[serde(default)]
    pub seven_day: UsageWindow,
    #[serde(default)]
    pub seven_day_opus: Option<UsageWindow>,
    #[serde(default)]
    pub seven_day_sonnet: Option<UsageWindow>,
}

/// Parse a usage-endpoint body. A body that isn't valid JSON is a hard error so
/// "endpoint changed shape" is loud, not a silent all-zero default. (Validate at
/// the trust boundary.) [S-07]
fn parse_claude_usage(body: &str) -> Result<ClaudeUsage, String> {
    serde_json::from_str(body).map_err(|e| format!("Unexpected usage response: {e}"))
}

/// GET the usage endpoint with a bearer token, returning the HTTP status
/// alongside a user message so callers can react to a 401 (refresh + retry)
/// without string-sniffing. `status == 0` is a network-level failure. We never
/// delete the account on an auth error (N2: recoverable errors must not destroy
/// stored creds).
async fn get_claude_usage_raw(token: &str) -> Result<ClaudeUsage, (u16, String)> {
    let resp = http_client()
        .get(CLAUDE_USAGE_URL)
        .bearer_auth(token)
        .header("anthropic-beta", CLAUDE_OAUTH_BETA)
        .header(reqwest::header::USER_AGENT, CLAUDE_USER_AGENT)
        .send()
        .await
        .map_err(|e| (0u16, format!("Cannot reach Anthropic: {e}")))?;

    let status = resp.status().as_u16();
    if !(200..300).contains(&status) {
        let body = match resp.text().await {
            Ok(b) => b,
            Err(e) => {
                // Not `unwrap_or_default()`: an unread error body is worth a line
                // in the log. The status still drives the user-facing message.
                log::warn!("email: usage error-body read failed ({e}); HTTP {status}");
                String::new()
            }
        };
        return Err((status, map_usage_error(status, &body)));
    }
    let body = resp.text().await.map_err(|e| (status, e.to_string()))?;
    parse_claude_usage(&body).map_err(|e| (status, e))
}

/// Message-only wrapper used by validate-on-connect.
async fn fetch_claude_usage_with_token(token: &str) -> Result<ClaudeUsage, String> {
    get_claude_usage_raw(token).await.map_err(|(_, m)| m)
}

/// Map a non-2xx usage response to a user-facing message. Pure → unit-tested.
/// 401 = token expired/invalid (refresh or reconnect). 403 = the token lacks a
/// required scope; regenerating the same kind of token won't help, so surface
/// Anthropic's own reason rather than a generic "reconnect". Anything else =
/// status + the endpoint's message.
fn map_usage_error(status: u16, body: &str) -> String {
    let detail = anthropic_error_message(body);
    match status {
        401 => "Claude token expired or invalid — reconnect the Claude account".to_string(),
        403 => match detail {
            Some(m) => format!("Claude token lacks a required permission: {m}"),
            None => "Claude token lacks a required permission for the usage endpoint".to_string(),
        },
        s => match detail {
            Some(m) => format!("Anthropic returned HTTP {s}: {m}"),
            None => format!("Anthropic returned HTTP {s}"),
        },
    }
}

/// Pull `error.message` (or a bare `error` string) out of an Anthropic error
/// body. Returns None when the body isn't the expected shape.
fn anthropic_error_message(body: &str) -> Option<String> {
    let v: serde_json::Value = serde_json::from_str(body).ok()?;
    let err = v.get("error")?;
    err.get("message")
        .and_then(|m| m.as_str())
        .or_else(|| err.as_str())
        .map(|s| s.to_string())
}

// ── OAuth (authorization-code + PKCE), manual code paste ────────────────────
//
// `claude setup-token` tokens lack `user:profile`, so Themia runs its own OAuth
// requesting that scope. There is no whitelisted loopback redirect for this
// client, so we use the hosted callback: after consent the page shows a
// `code#state` string the user copies back into Themia. [S-08]

#[derive(Serialize, Clone)]
pub struct ClaudeAuthStart {
    pub auth_url: String,
    pub state: String,
}

struct PendingClaudeAuth {
    verifier: String,
}

static CLAUDE_AUTH_PENDING: once_cell::sync::Lazy<Mutex<HashMap<String, PendingClaudeAuth>>> =
    once_cell::sync::Lazy::new(|| Mutex::new(HashMap::new()));

/// Build the authorization URL. `code=true` selects the manual copy-paste flow
/// whose hosted callback shows a `code#state` string. Pure → unit-tested.
fn build_claude_auth_url(challenge: &str, state: &str) -> String {
    format!(
        "{base}?code=true&response_type=code&client_id={cid}&redirect_uri={redirect}\
         &scope={scope}&code_challenge={challenge}&code_challenge_method=S256&state={state}",
        base = CLAUDE_AUTHORIZE_URL,
        cid = urlencoding::encode(CLAUDE_CLIENT_ID),
        redirect = urlencoding::encode(CLAUDE_REDIRECT_URI),
        scope = urlencoding::encode(CLAUDE_SCOPES),
        challenge = urlencoding::encode(challenge),
        state = urlencoding::encode(state),
    )
}

/// Split the pasted `code#state` value the callback page shows. The state part
/// is optional (some callback variants show the code alone); when present the
/// caller verifies it echoes the state we issued. Pure → unit-tested.
fn parse_claude_code(pasted: &str) -> Result<(String, Option<String>), String> {
    let s = pasted.trim();
    if s.is_empty() {
        return Err("Paste the code shown after you sign in".to_string());
    }
    match s.split_once('#') {
        Some((code, state)) => {
            let code = code.trim();
            if code.is_empty() {
                return Err(
                    "That code looks incomplete — copy the whole value the page shows".to_string(),
                );
            }
            let state = state.trim();
            Ok((
                code.to_string(),
                if state.is_empty() {
                    None
                } else {
                    Some(state.to_string())
                },
            ))
        }
        None => Ok((s.to_string(), None)),
    }
}

/// Begin the OAuth flow: generate PKCE + state, remember the verifier keyed by
/// state, and return the URL to open in the browser. [S-08]
pub fn start_claude_auth() -> Result<ClaudeAuthStart, String> {
    let verifier = pkce_verifier();
    let challenge = pkce_challenge(&verifier);
    let state = random_state();
    let auth_url = build_claude_auth_url(&challenge, &state);

    let mut guard = CLAUDE_AUTH_PENDING
        .lock()
        .unwrap_or_else(std::sync::PoisonError::into_inner);
    guard.insert(state.clone(), PendingClaudeAuth { verifier });

    Ok(ClaudeAuthStart { auth_url, state })
}

/// Finish the OAuth flow: exchange the pasted code for tokens, validate them
/// against the usage endpoint (so a bad grant never lands), and persist the
/// account with its refresh token. `state` is the value returned by
/// `start_claude_auth`. [S-08]
pub async fn complete_claude_auth(
    app: &tauri::AppHandle,
    code: String,
    state: String,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    let (code, echoed_state) = parse_claude_code(&code)?;

    let pending = {
        let mut guard = CLAUDE_AUTH_PENDING
            .lock()
            .unwrap_or_else(std::sync::PoisonError::into_inner);
        guard
            .remove(&state)
            .ok_or_else(|| "This sign-in expired. Start again.".to_string())?
    };

    // CSRF: if the callback echoed a state, it must match the one we issued.
    if let Some(echoed) = echoed_state {
        if echoed != state {
            return Err(
                "Sign-in could not be verified (state mismatch). Start again.".to_string(),
            );
        }
    }

    // Exchange the authorization code for tokens (form-urlencoded body).
    let resp = http_client()
        .post(CLAUDE_TOKEN_URL)
        .form(&[
            ("grant_type", "authorization_code"),
            ("code", code.as_str()),
            ("redirect_uri", CLAUDE_REDIRECT_URI),
            ("client_id", CLAUDE_CLIENT_ID),
            ("code_verifier", pending.verifier.as_str()),
            ("state", state.as_str()),
        ])
        .send()
        .await
        .map_err(|e| format!("Cannot reach Anthropic: {e}"))?;

    let status = resp.status();
    if !status.is_success() {
        let body = match resp.text().await {
            Ok(b) => b,
            Err(e) => {
                // Not `unwrap_or_default()`: log the read failure, then fall back
                // to the status for the user-facing message.
                log::warn!("email: token error-body read failed ({e}); HTTP {}", status.as_u16());
                String::new()
            }
        };
        let detail =
            anthropic_error_message(&body).unwrap_or_else(|| format!("HTTP {}", status.as_u16()));
        return Err(format!("Claude sign-in failed: {detail}"));
    }

    let tr: TokenResponse = resp
        .json()
        .await
        .map_err(|e| format!("Claude sign-in returned an unexpected response: {e}"))?;
    if let Some(err) = tr.error {
        return Err(format!("Claude sign-in failed: {err}"));
    }
    let access_token = tr
        .access_token
        .ok_or("Claude sign-in returned no access token")?;
    let refresh_token = tr.refresh_token;
    let expires_in = tr.expires_in.unwrap_or(3600);

    // Validate before persisting: the fresh token must be able to read usage.
    fetch_claude_usage_with_token(&access_token).await?;

    let id = "claude:default".to_string();
    let display_name = nickname
        .clone()
        .filter(|n| !n.trim().is_empty())
        .unwrap_or_else(|| "Claude Code".to_string());
    let data = AccountData {
        provider: PROVIDER_CLAUDE.to_string(),
        access_token,
        refresh_token,
        expires_at: now_secs() + expires_in,
        email: "Claude Code".to_string(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: None,
        port: None,
        username: None,
        password: None,
        caldav_url: None,
        carddav_url: None,
        api_base_url: None,
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email: "Claude Code".to_string(),
        display_name,
        nickname,
        provider: PROVIDER_CLAUDE.to_string(),
    })
}

/// Live usage for a stored Claude account. Refreshes the OAuth token when it is
/// (near-)expired, and retries once if the endpoint rejects it (401) — so a
/// token invalidated before its stored expiry self-heals. Never deletes the
/// account on an auth error (N2). [S-03]
pub async fn fetch_claude_usage(
    app: &tauri::AppHandle,
    account_id: &str,
) -> Result<ClaudeUsage, String> {
    let is_claude = load_accounts(app)
        .get(account_id)
        .map(|a| a.provider == PROVIDER_CLAUDE)
        .unwrap_or(false);
    if !is_claude {
        return Err("Claude account not found".to_string());
    }

    let token = get_valid_token(app, account_id).await?;
    match get_claude_usage_raw(&token).await {
        Ok(u) => Ok(u),
        Err((401, _)) => {
            // Rejected despite not being past its stored expiry: force one
            // refresh and retry once.
            let token = refresh_locked(app, account_id, true).await?;
            get_claude_usage_raw(&token).await.map_err(|(_, m)| m)
        }
        Err((_, m)) => Err(m),
    }
}

#[cfg(test)]
mod claude_usage_tests {
    use super::*;

    // Captured from a live GET /api/oauth/usage (Max 5x). Codename buckets and
    // extra_usage are present so the test pins that we ignore them. [S-07]
    const SAMPLE: &str = r#"{
      "five_hour": {"utilization": 42.5, "resets_at": "2026-06-14T15:00:00Z"},
      "seven_day": {"utilization": 8.0, "resets_at": null},
      "seven_day_oauth_apps": null,
      "seven_day_opus": null,
      "seven_day_sonnet": {"utilization": 3.0, "resets_at": null},
      "extra_usage": {"is_enabled": false, "monthly_limit": null, "used_credits": null}
    }"#;

    #[test]
    fn parses_windows_and_ignores_unknown_buckets() {
        let u = parse_claude_usage(SAMPLE).expect("valid body parses");
        assert_eq!(u.five_hour.utilization, 42.5);
        assert_eq!(
            u.five_hour.resets_at.as_deref(),
            Some("2026-06-14T15:00:00Z")
        );
        assert_eq!(u.seven_day.utilization, 8.0);
        assert_eq!(u.seven_day.resets_at, None);
        assert!(u.seven_day_opus.is_none());
        assert_eq!(u.seven_day_sonnet.expect("sonnet present").utilization, 3.0);
    }

    #[test]
    fn empty_object_is_all_zero_not_error() {
        let u = parse_claude_usage("{}").expect("empty object is valid");
        assert_eq!(u.five_hour.utilization, 0.0);
        assert_eq!(u.seven_day.resets_at, None);
        assert!(u.seven_day_sonnet.is_none());
    }

    #[test]
    fn malformed_body_is_error_not_default() {
        assert!(parse_claude_usage("not json at all").is_err());
    }

    // #72: the auth URL must request `user:profile` (the scope the usage endpoint
    // demands) and carry the client id + PKCE, or the whole feature 403s.
    #[test]
    fn auth_url_has_client_scope_and_pkce() {
        let url = build_claude_auth_url("CHAL", "STATE");
        assert!(url.starts_with("https://claude.ai/oauth/authorize?"));
        assert!(url.contains("client_id=9d1c250a-e61b-44d9-88ed-5944d1962f5e"));
        assert!(url.contains("code_challenge=CHAL"));
        assert!(url.contains("code_challenge_method=S256"));
        assert!(url.contains("state=STATE"));
        assert!(url.contains("user%3Aprofile"), "must request user:profile");
        assert!(url.contains("platform.claude.com%2Foauth%2Fcode%2Fcallback"));
    }

    #[test]
    fn parse_code_splits_code_and_state() {
        let (code, state) = parse_claude_code("  abc123#xyz789  ").expect("parses");
        assert_eq!(code, "abc123");
        assert_eq!(state.as_deref(), Some("xyz789"));
    }

    #[test]
    fn parse_code_without_hash_is_code_only() {
        let (code, state) = parse_claude_code("justcode").expect("parses");
        assert_eq!(code, "justcode");
        assert_eq!(state, None);
    }

    #[test]
    fn parse_code_empty_is_error() {
        assert!(parse_claude_code("   ").is_err());
    }

    // #72: 401 vs 403 must map to different guidance. A 403 scope problem must
    // NOT tell the user to just reconnect, and must surface Anthropic's reason.
    #[test]
    fn usage_error_401_is_reconnect_not_permission() {
        let m = map_usage_error(401, r#"{"error":{"message":"Invalid authentication credentials"}}"#);
        assert!(m.to_lowercase().contains("reconnect"));
        assert!(!m.to_lowercase().contains("permission"));
    }

    #[test]
    fn usage_error_403_surfaces_scope_reason() {
        let body = r#"{"error":{"type":"permission_error","message":"OAuth token does not meet scope requirement user:profile"}}"#;
        let m = map_usage_error(403, body);
        assert!(m.contains("user:profile"));
        assert!(!m.to_lowercase().contains("reconnect"));
    }

    #[test]
    fn anthropic_error_message_reads_nested_and_bare() {
        assert_eq!(
            anthropic_error_message(r#"{"error":{"message":"boom"}}"#).as_deref(),
            Some("boom")
        );
        assert_eq!(
            anthropic_error_message(r#"{"error":"invalid_grant"}"#).as_deref(),
            Some("invalid_grant")
        );
        assert_eq!(anthropic_error_message("not json"), None);
    }

    #[test]
    fn token_response_parses_oauth_grant() {
        let tr: TokenResponse = serde_json::from_str(
            r#"{"access_token":"sk-ant-oat01-AAA","refresh_token":"sk-ant-ort01-BBB","expires_in":3600}"#,
        )
        .expect("valid token response");
        assert_eq!(tr.access_token.as_deref(), Some("sk-ant-oat01-AAA"));
        assert!(tr.refresh_token.unwrap().starts_with("sk-ant-ort01-"));
        assert_eq!(tr.expires_in, Some(3600));
    }
}

// ---------------------------------------------------------------------------
// Add RSS feed
//
// Feeds are credential-free "accounts" with provider "rss": the feed URL lives
// in the `email` field and the feed's own title in `display_name`. Reuses the
// shared account store, so list/rename/sign-out all work unchanged.
// ---------------------------------------------------------------------------

/// Extract the first `<title>…</title>` from feed XML, trimmed. Returns None
/// when absent or empty. Good enough for RSS 2.0 (`channel > title`) and Atom
/// (`feed > title`), since the channel/feed title is the first one in the doc.
/// Content negotiation for a feed fetch. Some hosts serve the human HTML page
/// for `Accept: */*` and the XML only when asked for it, which parses to zero
/// items and looks like an empty feed.
///
/// Kept byte-identical to `RSS_ACCEPT` in `widgets/online/rss/feed.ts`: the
/// add-a-feed validation here and the widget's refresh there must ask the host
/// the same question, or a feed can validate and then never refresh (#81).
const RSS_ACCEPT: &str =
    "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, */*;q=0.8";

fn parse_feed_title(body: &str) -> Option<String> {
    // Search the ORIGINAL `body` with a case-insensitive ASCII match so every
    // returned offset is a valid char boundary in `body`. Previously offsets
    // came from `body.to_lowercase()`, whose byte length can differ from the
    // original (e.g. `İ` lowercases to two bytes), so slicing `body` with those
    // offsets could land mid-character and panic on a malformed/untrusted feed.
    let start_tag = find_ci_ascii(body, "<title")?;
    // Skip to the end of the opening tag (handles attributes / CDATA wrappers).
    let after_open = body[start_tag..].find('>')? + start_tag + 1;
    let end = find_ci_ascii(&body[after_open..], "</title>")? + after_open;
    let raw = body[after_open..end].trim();
    let cleaned = raw
        .trim_start_matches("<![CDATA[")
        .trim_end_matches("]]>")
        .trim();
    if cleaned.is_empty() {
        None
    } else {
        Some(cleaned.to_string())
    }
}

pub async fn add_rss_feed(
    app: &tauri::AppHandle,
    url: String,
    nickname: Option<String>,
) -> Result<AccountInfo, String> {
    let url = url.trim().to_string();
    if url.is_empty() {
        return Err("Feed URL is required".to_string());
    }

    // The SAME identity and the SAME content negotiation the widget's refresh
    // uses (netproxy [PX-10], `http_get`). These disagreed until #81: adding a
    // feed sent `themia-app` and refreshing it sent a truncated browser string,
    // so a host's bot filter could accept the add and challenge every refresh
    // afterwards - which looks exactly like "the feed stopped updating".
    let client = http_client_ua(widget_netproxy::user_agent());

    let resp = client
        .get(&url)
        .header(reqwest::header::ACCEPT, RSS_ACCEPT)
        .send()
        .await
        .map_err(|e| format!("Cannot reach {}: {}", url, e))?;

    if !resp.status().is_success() {
        return Err(format!("Feed returned HTTP {}", resp.status().as_u16()));
    }

    let body = resp
        .text()
        .await
        .map_err(|e| format!("Couldn't read feed: {}", e))?;

    if !(body.contains("<rss") || body.contains("<feed") || body.contains("<channel")) {
        return Err("That URL doesn't look like an RSS or Atom feed".to_string());
    }

    let display_name = parse_feed_title(&body).unwrap_or_else(|| url_host(&url));
    let id = format!("rss:{}", url);

    let data = AccountData {
        provider: "rss".to_string(),
        access_token: String::new(),
        refresh_token: None,
        expires_at: 0,
        email: url.clone(),
        display_name: display_name.clone(),
        nickname: nickname.clone(),
        host: None,
        port: None,
        username: None,
        password: None,
        caldav_url: None,
        carddav_url: None,
        api_base_url: None,
    };

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    accounts.insert(id.clone(), data);
    save_accounts(app, &accounts);
    drop(_w);

    Ok(AccountInfo {
        id,
        email: url,
        display_name,
        nickname,
        provider: "rss".to_string(),
    })
}

// ---------------------------------------------------------------------------
// Device code auth (Microsoft)
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct DeviceCodeResponse {
    device_code: String,
    user_code: String,
    verification_uri: String,
    interval: u64,
}

pub async fn start_device_auth() -> Result<DeviceAuthStart, String> {
    let url = format!(
        "https://login.microsoftonline.com/{}/oauth2/v2.0/devicecode",
        TENANT
    );
    let client = http_client();
    let resp = client
        .post(&url)
        .form(&[("client_id", CLIENT_ID), ("scope", SCOPE)])
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let body = resp.text().await.unwrap_or_default();
        return Err(format!("Device code request failed: {}", body));
    }

    let dc: DeviceCodeResponse = resp.json().await.map_err(|e| e.to_string())?;
    Ok(DeviceAuthStart {
        user_code: dc.user_code,
        verification_url: dc.verification_uri,
        device_code: dc.device_code,
        interval: dc.interval,
    })
}

#[derive(Deserialize)]
struct TokenResponse {
    access_token: Option<String>,
    refresh_token: Option<String>,
    expires_in: Option<u64>,
    error: Option<String>,
}

pub async fn poll_device_auth(
    app: &tauri::AppHandle,
    device_code: &str,
) -> Result<Option<AccountInfo>, String> {
    let url = format!(
        "https://login.microsoftonline.com/{}/oauth2/v2.0/token",
        TENANT
    );
    let client = http_client();
    let resp = client
        .post(&url)
        .form(&[
            ("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            ("client_id", CLIENT_ID),
            ("device_code", device_code),
        ])
        .send()
        .await
        .map_err(|e| e.to_string())?;

    let tr: TokenResponse = resp.json().await.map_err(|e| e.to_string())?;

    if let Some(err) = tr.error {
        return if err == "authorization_pending" || err == "slow_down" {
            Ok(None)
        } else {
            Err(format!("Auth error: {}", err))
        };
    }

    let access_token = tr.access_token.ok_or("No access token received")?;

    let me_resp = client
        .get("https://graph.microsoft.com/v1.0/me?$select=id,mail,displayName,userPrincipalName")
        .bearer_auth(&access_token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !me_resp.status().is_success() {
        let status = me_resp.status().as_u16();
        let body = me_resp.text().await.unwrap_or_default();
        return Err(format!("Failed to get user profile ({}): {}", status, body));
    }

    let me: serde_json::Value = me_resp.json().await.map_err(|e| e.to_string())?;

    let id = me["id"].as_str().unwrap_or("unknown").to_string();
    let email = me["mail"]
        .as_str()
        .or_else(|| me["userPrincipalName"].as_str())
        .unwrap_or(&id)
        .to_string();
    let display_name = me["displayName"].as_str().unwrap_or(&email).to_string();

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    let existing_nick = accounts.get(&id).and_then(|a| a.nickname.clone());
    accounts.insert(
        id.clone(),
        AccountData {
            provider: "microsoft".to_string(),
            access_token,
            refresh_token: tr.refresh_token,
            expires_at: now_secs() + tr.expires_in.unwrap_or(3600),
            email: email.clone(),
            display_name: display_name.clone(),
            nickname: existing_nick,
            host: None,
            port: None,
            username: None,
            password: None,
            caldav_url: None,
            carddav_url: None,
            api_base_url: None,
        },
    );
    save_accounts(app, &accounts);

    Ok(Some(AccountInfo {
        id,
        email,
        display_name,
        nickname: None,
        provider: "microsoft".to_string(),
    }))
}

// ---------------------------------------------------------------------------
// Google OAuth (loopback redirect + PKCE, desktop client)
// ---------------------------------------------------------------------------

#[derive(Serialize, Clone)]
pub struct GoogleAuthStart {
    pub auth_url: String,
    pub state: String,
}

#[derive(Deserialize)]
struct GoogleUserInfo {
    sub: String,
    email: Option<String>,
    name: Option<String>,
}

struct PendingGoogleAuth {
    verifier: String,
    redirect_uri: String,
    code_rx: tokio::sync::Mutex<Option<tokio::sync::oneshot::Receiver<Result<String, String>>>>,
}

static GOOGLE_AUTH_PENDING: once_cell::sync::Lazy<
    Mutex<HashMap<String, std::sync::Arc<PendingGoogleAuth>>>,
> = once_cell::sync::Lazy::new(|| Mutex::new(HashMap::new()));

fn pkce_verifier() -> String {
    use rand::Rng;
    const CHARSET: &[u8] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
    let mut rng = rand::thread_rng();
    (0..64)
        .map(|_| CHARSET[rng.gen_range(0..CHARSET.len())] as char)
        .collect()
}

fn pkce_challenge(verifier: &str) -> String {
    use base64::Engine as _;
    use sha2::{Digest, Sha256};
    let digest = Sha256::digest(verifier.as_bytes());
    base64::engine::general_purpose::URL_SAFE_NO_PAD.encode(digest)
}

fn random_state() -> String {
    use rand::Rng;
    let mut rng = rand::thread_rng();
    (0..32)
        .map(|_| format!("{:02x}", rng.gen::<u8>()))
        .collect()
}

/// Parse `code` and `state` from a raw HTTP request line like
/// `GET /?code=abc&state=xyz HTTP/1.1`.
fn parse_oauth_redirect(request_line: &str) -> Option<(String, String)> {
    let path = request_line.split_whitespace().nth(1)?;
    let (_, query) = path.split_once('?')?;
    let mut code = None;
    let mut state = None;
    let mut error: Option<String> = None;
    for pair in query.split('&') {
        // Skip a malformed/empty pair instead of aborting the whole parse: only
        // code/state/error matter, and a stray `&` or valueless param shouldn't
        // throw the entire redirect away.
        let Some((k, v)) = pair.split_once('=') else {
            continue;
        };
        let Ok(decoded) = urlencoding::decode(v).map(|s| s.into_owned()) else {
            continue;
        };
        match k {
            "code" => code = Some(decoded),
            "state" => state = Some(decoded),
            "error" => error = Some(decoded),
            _ => {}
        }
    }
    if let Some(e) = error {
        return Some((format!("__ERROR__{e}"), state.unwrap_or_default()));
    }
    Some((code?, state?))
}

/// The Desktop client's secret, or why this build cannot sign in to Google
/// ([S-53]).
fn google_client_secret() -> Result<&'static str, String> {
    match GOOGLE_CLIENT_SECRET {
        Some(secret) if !secret.is_empty() => Ok(secret),
        _ => Err("Google sign-in is not configured in this build. Build with \
                  THEMIA_GOOGLE_CLIENT_SECRET set to the Desktop OAuth client's secret."
            .into()),
    }
}

/// Starts a loopback HTTP listener on a free port, builds the Google
/// authorization URL, and returns it. The caller should open the URL in the
/// user's browser and then invoke `complete_google_auth(state)` to finish.
pub async fn start_google_auth() -> Result<GoogleAuthStart, String> {
    if GOOGLE_CLIENT_ID == "REPLACE_WITH_GOOGLE_DESKTOP_CLIENT_ID" {
        return Err(
            "Google client ID is not configured. Register a Desktop OAuth client \
             in Google Cloud Console and set GOOGLE_CLIENT_ID in widgets/office/email/src/lib.rs."
                .into(),
        );
    }
    // Fail before the browser round trip, not at the token exchange.
    google_client_secret()?;

    let listener = std::net::TcpListener::bind("127.0.0.1:0")
        .map_err(|e| format!("Cannot bind loopback: {e}"))?;
    listener.set_nonblocking(false).map_err(|e| e.to_string())?;
    let port = listener.local_addr().map_err(|e| e.to_string())?.port();

    let verifier = pkce_verifier();
    let challenge = pkce_challenge(&verifier);
    let state = random_state();
    let redirect_uri = format!("http://127.0.0.1:{port}");

    let (tx, rx) = tokio::sync::oneshot::channel::<Result<String, String>>();

    // Accept a single redirect callback on a blocking OS thread.
    let state_for_thread = state.clone();
    std::thread::spawn(move || {
        use std::io::{Read, Write};

        // Give the user a bounded window to complete the browser flow.
        let _ = listener.set_nonblocking(true);
        let deadline = std::time::Instant::now() + std::time::Duration::from_secs(300);
        let stream_result = loop {
            match listener.accept() {
                Ok((s, _)) => break Ok(s),
                Err(ref e) if e.kind() == std::io::ErrorKind::WouldBlock => {
                    if std::time::Instant::now() >= deadline {
                        break Err("Timed out waiting for OAuth callback".to_string());
                    }
                    std::thread::sleep(std::time::Duration::from_millis(200));
                }
                Err(e) => break Err(format!("Accept failed: {e}")),
            }
        };

        let mut stream = match stream_result {
            Ok(s) => s,
            Err(e) => {
                let _ = tx.send(Err(e));
                return;
            }
        };

        let _ = stream.set_nonblocking(false);
        let _ = stream.set_read_timeout(Some(std::time::Duration::from_secs(10)));

        // Read until the first request line is complete (a newline arrives), not
        // a single fixed read: the GET line can be split across TCP segments, and
        // a lone first segment would otherwise yield a truncated, unparseable
        // line. Capped + bounded by the 10s read timeout above.
        let mut acc: Vec<u8> = Vec::with_capacity(4096);
        let mut chunk = [0u8; 1024];
        loop {
            match stream.read(&mut chunk) {
                Ok(0) => break,
                Ok(n) => {
                    acc.extend_from_slice(&chunk[..n]);
                    if acc.contains(&b'\n') || acc.len() >= 8192 {
                        break;
                    }
                }
                Err(_) => break,
            }
        }
        let request = String::from_utf8_lossy(&acc).into_owned();
        let first_line = request.lines().next().unwrap_or("").to_string();

        let (ok_msg, result): (&str, Result<String, String>) = match parse_oauth_redirect(
            &first_line,
        ) {
            Some((code_or_err, recv_state)) if recv_state == state_for_thread => {
                if let Some(err) = code_or_err.strip_prefix("__ERROR__") {
                    (
                        "<h2>Sign-in failed.</h2><p>You can close this tab.</p>",
                        Err(format!("Google returned error: {err}")),
                    )
                } else {
                    (
                        "<h2>Sign-in complete.</h2><p>You can close this tab and return to the app.</p>",
                        Ok(code_or_err),
                    )
                }
            }
            _ => (
                "<h2>Sign-in failed.</h2><p>State mismatch. You can close this tab.</p>",
                Err("OAuth state mismatch or missing code".into()),
            ),
        };

        let body = format!(
            "<!doctype html><html><body style=\"font-family:sans-serif;padding:2rem;\">{ok_msg}</body></html>"
        );
        let response = format!(
            "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
            body.len(),
            body
        );
        let _ = stream.write_all(response.as_bytes());
        let _ = stream.flush();

        let _ = tx.send(result);
    });

    let pending = std::sync::Arc::new(PendingGoogleAuth {
        verifier,
        redirect_uri: redirect_uri.clone(),
        code_rx: tokio::sync::Mutex::new(Some(rx)),
    });
    GOOGLE_AUTH_PENDING
        .lock()
        .map_err(|e| e.to_string())?
        .insert(state.clone(), pending);

    let auth_url = format!(
        "https://accounts.google.com/o/oauth2/v2/auth?\
         response_type=code\
         &client_id={client}\
         &redirect_uri={redirect}\
         &scope={scope}\
         &state={state}\
         &code_challenge={challenge}\
         &code_challenge_method=S256\
         &access_type=offline\
         &prompt=consent",
        client = urlencoding::encode(GOOGLE_CLIENT_ID),
        redirect = urlencoding::encode(&redirect_uri),
        scope = urlencoding::encode(GOOGLE_SCOPE),
        state = urlencoding::encode(&state),
        challenge = challenge,
    );

    Ok(GoogleAuthStart { auth_url, state })
}

/// Waits for the loopback callback to fire, exchanges the authorization code
/// for tokens, fetches the user profile, and persists the account.
pub async fn complete_google_auth(
    app: &tauri::AppHandle,
    state: &str,
) -> Result<AccountInfo, String> {
    let pending = {
        let mut guard = GOOGLE_AUTH_PENDING.lock().map_err(|e| e.to_string())?;
        guard
            .remove(state)
            .ok_or_else(|| "Unknown OAuth state".to_string())?
    };

    let rx = {
        let mut rx_guard = pending.code_rx.lock().await;
        rx_guard
            .take()
            .ok_or_else(|| "OAuth already completed".to_string())?
    };

    let code = rx
        .await
        .map_err(|_| "OAuth callback cancelled".to_string())??;

    // Exchange code → tokens
    let client = http_client();
    let resp = client
        .post("https://oauth2.googleapis.com/token")
        .form(&[
            ("grant_type", "authorization_code"),
            ("code", code.as_str()),
            ("client_id", GOOGLE_CLIENT_ID),
            ("client_secret", google_client_secret()?),
            ("code_verifier", pending.verifier.as_str()),
            ("redirect_uri", pending.redirect_uri.as_str()),
        ])
        .send()
        .await
        .map_err(|e| e.to_string())?;

    let tr: TokenResponse = resp.json().await.map_err(|e| e.to_string())?;
    if let Some(err) = tr.error {
        return Err(format!("Google token exchange failed: {err}"));
    }

    let access_token = tr.access_token.ok_or("No access token from Google")?;
    let refresh_token = tr.refresh_token;
    let expires_in = tr.expires_in.unwrap_or(3600);

    // Fetch userinfo
    let me_resp = client
        .get("https://www.googleapis.com/oauth2/v3/userinfo")
        .bearer_auth(&access_token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !me_resp.status().is_success() {
        let s = me_resp.status().as_u16();
        let body = me_resp.text().await.unwrap_or_default();
        return Err(format!("Google userinfo failed ({s}): {body}"));
    }

    let me: GoogleUserInfo = me_resp.json().await.map_err(|e| e.to_string())?;
    let email = me.email.clone().unwrap_or_else(|| me.sub.clone());
    let display_name = me.name.unwrap_or_else(|| email.clone());
    let id = format!("google:{}", me.sub);

    let _w = account_store_lock();
    let mut accounts = load_accounts(app);
    let existing_nick = accounts.get(&id).and_then(|a| a.nickname.clone());

    accounts.insert(
        id.clone(),
        AccountData {
            provider: "google".to_string(),
            access_token,
            refresh_token,
            expires_at: now_secs() + expires_in,
            email: email.clone(),
            display_name: display_name.clone(),
            nickname: existing_nick.clone(),
            host: None,
            port: None,
            username: None,
            password: None,
            caldav_url: None,
            carddav_url: None,
            api_base_url: None,
        },
    );
    save_accounts(app, &accounts);

    Ok(AccountInfo {
        id,
        email,
        display_name,
        nickname: existing_nick,
        provider: "google".to_string(),
    })
}

// ---------------------------------------------------------------------------
// Token refresh (Microsoft + Google + Claude)
// ---------------------------------------------------------------------------

async fn get_valid_token(app: &tauri::AppHandle, account_id: &str) -> Result<String, String> {
    // Fast path: a still-valid cached token needs no lock and no refresh.
    {
        let accounts = load_accounts(app);
        let account = accounts
            .get(account_id)
            .ok_or_else(|| "Account not found".to_string())?;
        if account.expires_at > now_secs() + 300 {
            return Ok(account.access_token.clone());
        }
    }

    refresh_locked(app, account_id, false).await
}

/// Refresh THIS account's token under its per-account lock (N3 single-flight).
/// With `force`, refresh even when the cached token still looks valid — used to
/// recover from a token the provider invalidated before its stored expiry.
async fn refresh_locked(
    app: &tauri::AppHandle,
    account_id: &str,
    force: bool,
) -> Result<String, String> {
    let lock = refresh_lock_for(account_id);
    let _guard = lock.lock().await;

    // Double-check under the lock: another task may have refreshed while we were
    // waiting. Skip this early-out when forcing (the token is bad, not stale).
    let account = {
        let accounts = load_accounts(app);
        let account = accounts
            .get(account_id)
            .ok_or_else(|| "Account not found".to_string())?
            .clone();
        if !force && account.expires_at > now_secs() + 300 {
            return Ok(account.access_token);
        }
        account
    };

    let refresh = account
        .refresh_token
        .clone()
        .ok_or("No refresh token; please sign in again")?;

    let client = http_client();
    let resp = match account.provider.as_str() {
        "google" => client
            .post("https://oauth2.googleapis.com/token")
            .form(&[
                ("grant_type", "refresh_token"),
                ("client_id", GOOGLE_CLIENT_ID),
                ("client_secret", google_client_secret()?),
                ("refresh_token", refresh.as_str()),
            ])
            .send()
            .await
            .map_err(|e| e.to_string())?,
        "claude" => client
            .post(CLAUDE_TOKEN_URL)
            .form(&[
                ("grant_type", "refresh_token"),
                ("client_id", CLAUDE_CLIENT_ID),
                ("refresh_token", refresh.as_str()),
            ])
            .send()
            .await
            .map_err(|e| e.to_string())?,
        _ => {
            let ms_url = format!(
                "https://login.microsoftonline.com/{}/oauth2/v2.0/token",
                TENANT
            );
            client
                .post(&ms_url)
                .form(&[
                    ("grant_type", "refresh_token"),
                    ("client_id", CLIENT_ID),
                    ("refresh_token", refresh.as_str()),
                ])
                .send()
                .await
                .map_err(|e| e.to_string())?
        }
    };

    finalize_refresh(app, account_id, account, refresh, resp).await
}

/// Common refresh-response handler: parses the token response, persists the new
/// access/refresh token under the account's original provider, and returns the
/// new access token.
async fn finalize_refresh(
    app: &tauri::AppHandle,
    account_id: &str,
    // The pre-refresh snapshot is intentionally NOT used to rebuild the stored
    // entry: we re-load and mutate only the token fields under the lock so a
    // concurrent rename/re-auth isn't clobbered (see below).
    _account: AccountData,
    refresh: String,
    resp: reqwest::Response,
) -> Result<String, String> {
    let tr: TokenResponse = resp.json().await.map_err(|e| e.to_string())?;

    if let Some(err) = tr.error {
        // Do NOT delete the account on a refresh error: it may be transient
        // (network blip, temporary provider issue) and deleting destroys the
        // user's stored config + credentials. Surface the error so the user can
        // reconnect; the account stays put. (N2)
        return Err(format!("Token refresh failed: {}", err));
    }

    let new_access = tr
        .access_token
        .ok_or("No access token in refresh response")?;
    // Whether the provider rotated the refresh token. If it did, persisting it is
    // load-bearing: losing the write bricks the account on next launch.
    let rotated = tr.refresh_token.is_some();
    let new_refresh = tr.refresh_token.or(Some(refresh));
    let new_expires = now_secs() + tr.expires_in.unwrap_or(3600);

    // Single-writer read-modify-write (N3). Re-load under the lock and mutate ONLY
    // the token fields on the CURRENT stored entry, so a concurrent rename / re-
    // auth that happened during the refresh round-trip is preserved instead of
    // being clobbered by the pre-refresh snapshot (lost-update fix).
    {
        let _w = account_store_lock();
        let mut accounts = load_accounts(app);
        match accounts.get_mut(account_id) {
            Some(acc) => {
                acc.access_token = new_access.clone();
                acc.refresh_token = new_refresh;
                acc.expires_at = new_expires;
            }
            // Signed out mid-refresh: do not resurrect the account.
            None => return Err("Account was removed during token refresh".into()),
        }
        // Persisting a rotated refresh token is load-bearing, so write through and
        // surface a failure for rotating providers rather than the best-effort
        // save_accounts (which silently drops write errors). A non-rotated refresh
        // can tolerate a dropped write: the old refresh token still works next
        // launch, so we don't fail the user's current action for it.
        if let Err(e) = save_accounts_at(&accounts_path(app), &accounts) {
            if rotated {
                return Err(format!("Could not save the refreshed token: {e}"));
            }
            // ignore: token not rotated; next launch re-refreshes with the same
            // still-valid refresh token, so a transient write failure is harmless.
        }
    }

    Ok(new_access)
}

// ---------------------------------------------------------------------------
// IMAP helpers
// ---------------------------------------------------------------------------

fn imap_folder_name(folder: &str) -> &str {
    match folder {
        "inbox" => "INBOX",
        "sent" | "sentitems" => "Sent",
        "drafts" => "Drafts",
        "deleteditems" => "Trash",
        "junkemail" => "Junk",
        other => other,
    }
}

/// IMAP SPECIAL-USE attribute (RFC 6154) for a logical folder, if one applies.
fn folder_special_use(folder: &str) -> Option<&'static str> {
    match folder {
        "sent" | "sentitems" => Some("\\Sent"),
        "drafts" => Some("\\Drafts"),
        "deleteditems" => Some("\\Trash"),
        "junkemail" => Some("\\Junk"),
        "archive" => Some("\\Archive"),
        _ => None,
    }
}

/// Fallback mailbox-name candidates per logical folder, tried in order when the
/// server advertises no SPECIAL-USE attribute. Covers common server layouts
/// including Gmail's `[Gmail]/…` hierarchy.
fn folder_candidates(folder: &str) -> &'static [&'static str] {
    match folder {
        "sent" | "sentitems" => &["Sent", "Sent Items", "Sent Mail", "[Gmail]/Sent Mail"],
        "drafts" => &["Drafts", "[Gmail]/Drafts"],
        "deleteditems" => &[
            "Trash",
            "Deleted",
            "Deleted Items",
            "Bin",
            "[Gmail]/Trash",
            "[Gmail]/Bin",
        ],
        "junkemail" => &["Junk", "Spam", "Junk E-mail", "Junk Email", "[Gmail]/Spam"],
        "archive" => &["Archive", "[Gmail]/All Mail", "All Mail"],
        _ => &[],
    }
}

/// Resolve a logical folder to a real IMAP mailbox name on this account.
///
/// INBOX is constant. Otherwise prefer the mailbox the server tags with the
/// matching SPECIAL-USE attribute (RFC 6154): this is what makes Gmail's
/// `[Gmail]/Sent Mail`, Outlook's `Sent Items`, and Dovecot's `Sent` all work
/// without per-provider hacks. If the server advertises none, fall back to the
/// first candidate name that actually exists, then to the first candidate, then
/// to the legacy bare mapping.
fn resolve_imap_mailbox<T: std::io::Read + std::io::Write>(
    session: &mut imap::Session<T>,
    folder: &str,
) -> String {
    if folder.eq_ignore_ascii_case("inbox") {
        return "INBOX".to_string();
    }

    let names = session.list(None, Some("*")).ok();

    // 1. SPECIAL-USE match.
    if let (Some(flag), Some(names)) = (folder_special_use(folder), names.as_ref()) {
        if let Some(n) = names.iter().find(|n| {
            n.attributes().iter().any(|a| match a {
                imap::types::NameAttribute::Custom(s) => s.eq_ignore_ascii_case(flag),
                _ => false,
            })
        }) {
            return n.name().to_string();
        }
    }

    // 2. First candidate name that exists on the server.
    let candidates = folder_candidates(folder);
    if let Some(names) = names.as_ref() {
        if let Some(c) = candidates
            .iter()
            .find(|c| names.iter().any(|n| n.name().eq_ignore_ascii_case(c)))
        {
            return (*c).to_string();
        }
    }

    // 3. Last resort: first candidate, else the legacy bare mapping.
    candidates
        .first()
        .map(|c| (*c).to_string())
        .unwrap_or_else(|| imap_folder_name(folder).to_string())
}

/// Parse a single header field value from raw RFC 5322 header bytes.
/// Handles header folding (continuation lines).
fn parse_header_field(raw: &str, field: &str) -> String {
    decode_mime_words(&parse_header_field_raw(raw, field))
}

/// Same as `parse_header_field` but WITHOUT RFC 2047 encoded-word decoding.
/// Structured headers (`Content-Type`, `Content-Transfer-Encoding`,
/// `Content-Disposition`) are never encoded-words, and their parameters may
/// legitimately contain `=?` — a MIME boundary permits both characters (RFC
/// 2046 §5.1.1) — so running the decoder over them can only corrupt the value.
fn parse_header_field_raw(raw: &str, field: &str) -> String {
    let field_lower = format!("{}:", field.to_lowercase());
    let mut result = String::new();
    let mut capturing = false;

    for line in raw.lines() {
        if line.starts_with(' ') || line.starts_with('\t') {
            if capturing {
                result.push(' ');
                result.push_str(line.trim());
            }
        } else {
            let line_lower = line.to_lowercase();
            if line_lower.starts_with(&field_lower) {
                result = line[field_lower.len()..].trim().to_string();
                capturing = true;
            } else {
                capturing = false;
            }
        }
    }

    result
}

/// Decode RFC 2047 encoded words (=?charset?B/Q?...?=)
fn decode_mime_words(s: &str) -> String {
    let mut result = String::new();
    let mut remaining = s;
    let mut prev_was_encoded = false;

    while let Some(start) = remaining.find("=?") {
        let before = &remaining[..start];
        // Only omit whitespace between consecutive encoded words
        if prev_was_encoded && before.trim().is_empty() {
            // skip inter-word whitespace
        } else {
            result.push_str(before);
        }
        remaining = &remaining[start..];

        if let Some(end) = remaining.find("?=") {
            let word = &remaining[..end + 2];
            result.push_str(&decode_encoded_word(word));
            remaining = &remaining[end + 2..];
            prev_was_encoded = true;
        } else {
            result.push_str(remaining);
            return result;
        }
    }

    result.push_str(remaining);
    result
}

fn decode_encoded_word(word: &str) -> String {
    // word: =?charset?B?text?= or =?charset?Q?text?=
    if word.len() < 7 {
        return word.to_string();
    }
    let inner = &word[2..word.len() - 2];
    let parts: Vec<&str> = inner.splitn(3, '?').collect();
    if parts.len() != 3 {
        return word.to_string();
    }

    let _charset = parts[0];
    let encoding = parts[1].to_uppercase();
    let text = parts[2];

    match encoding.as_str() {
        "B" => {
            let clean: String = text.chars().filter(|c| !c.is_whitespace()).collect();
            base64_decode_str(&clean).unwrap_or_else(|| text.to_string())
        }
        "Q" => decode_qp(text),
        _ => text.to_string(),
    }
}

fn decode_qp(s: &str) -> String {
    let s = s.replace('_', " ");
    let bytes = s.as_bytes();
    let mut out: Vec<u8> = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'=' && i + 2 < bytes.len() {
            if let Ok(hex) = std::str::from_utf8(&bytes[i + 1..i + 3]) {
                if let Ok(b) = u8::from_str_radix(hex, 16) {
                    out.push(b);
                    i += 3;
                    continue;
                }
            }
        }
        out.push(bytes[i]);
        i += 1;
    }
    String::from_utf8_lossy(&out).into_owned()
}

/// Decode a quoted-printable **body** (RFC 2045 §6.7) to raw bytes: `=XX` hex
/// escapes plus soft line breaks (a trailing `=` that joins wrapped lines).
/// Distinct from `decode_qp`, which decodes an RFC 2047 encoded-*word*: there
/// `_` means space and soft breaks cannot occur, while in a body `_` is a
/// literal underscore. Bytes, not `String`, because the part's charset decides
/// how they become text (`decode_charset_bytes`).
fn decode_qp_body_bytes(s: &str) -> Vec<u8> {
    let bytes = s.as_bytes();
    let mut out: Vec<u8> = Vec::with_capacity(bytes.len());
    let mut i = 0;
    while i < bytes.len() {
        if bytes[i] == b'=' {
            // Soft line break: `=\n` / `=\r\n` — the break itself is dropped.
            if i + 1 < bytes.len() && bytes[i + 1] == b'\n' {
                i += 2;
                continue;
            }
            if i + 2 < bytes.len() && bytes[i + 1] == b'\r' && bytes[i + 2] == b'\n' {
                i += 3;
                continue;
            }
            if i + 2 < bytes.len() {
                if let Ok(hex) = std::str::from_utf8(&bytes[i + 1..i + 3]) {
                    if let Ok(b) = u8::from_str_radix(hex, 16) {
                        out.push(b);
                        i += 3;
                        continue;
                    }
                }
            }
            // A bare `=` cannot appear in well-formed QP; at the very end it is
            // a soft break whose newline was cut off by truncation. Drop it.
            if i + 1 >= bytes.len() {
                break;
            }
        }
        out.push(bytes[i]);
        i += 1;
    }
    out
}

fn base64_decode_str(s: &str) -> Option<String> {
    String::from_utf8(base64_decode_bytes(s)?).ok()
}

fn base64_decode_bytes(s: &str) -> Option<Vec<u8>> {
    const TABLE: &[u8; 64] = b"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    let mut lookup = [0xffu8; 256];
    for (i, &c) in TABLE.iter().enumerate() {
        lookup[c as usize] = i as u8;
    }

    // Padding and (in a body) the line breaks that wrap the stream are noise.
    let input: Vec<u8> = s
        .bytes()
        .filter(|&b| b != b'=' && !b.is_ascii_whitespace())
        .collect();
    let mut out: Vec<u8> = Vec::with_capacity(input.len() * 3 / 4);
    let mut i = 0;

    while i + 3 < input.len() {
        let a = lookup[input[i] as usize] as u32;
        let b = lookup[input[i + 1] as usize] as u32;
        let c = lookup[input[i + 2] as usize] as u32;
        let d = lookup[input[i + 3] as usize] as u32;
        if a == 0xff || b == 0xff || c == 0xff || d == 0xff {
            return None;
        }
        let val = (a << 18) | (b << 12) | (c << 6) | d;
        out.push((val >> 16) as u8);
        out.push((val >> 8) as u8);
        out.push(val as u8);
        i += 4;
    }

    // Handle remaining 2 or 3 bytes
    if i + 2 < input.len() {
        let a = lookup[input[i] as usize] as u32;
        let b = lookup[input[i + 1] as usize] as u32;
        let c = lookup[input[i + 2] as usize] as u32;
        if a != 0xff && b != 0xff && c != 0xff {
            out.push((a << 2 | b >> 4) as u8);
            out.push((b << 4 | c >> 2) as u8);
        }
    } else if i + 1 < input.len() {
        let a = lookup[input[i] as usize] as u32;
        let b = lookup[input[i + 1] as usize] as u32;
        if a != 0xff && b != 0xff {
            out.push((a << 2 | b >> 4) as u8);
        }
    }

    Some(out)
}

/// Parse "Name <email>" or just "email" from a From: header value.
fn parse_from_field(from: &str) -> (String, String) {
    let from = from.trim();
    if let Some(lt) = from.rfind('<') {
        let name = from[..lt].trim().trim_matches('"').to_string();
        let email = from[lt + 1..].trim_end_matches('>').trim().to_string();
        (name, email)
    } else {
        (from.to_string(), from.to_string())
    }
}

/// Convert RFC 2822 date string to ISO 8601 (best-effort).
fn parse_rfc2822_date(date: &str) -> String {
    // "Mon, 01 Apr 2026 12:00:00 +0000"
    let s = if let Some(pos) = date.find(',') {
        date[pos + 1..].trim()
    } else {
        date.trim()
    };

    let parts: Vec<&str> = s.split_whitespace().collect();
    if parts.len() < 5 {
        return date.to_string();
    }

    let day: u32 = parts[0].parse().unwrap_or(1);
    let month: u32 = match parts[1].to_lowercase().as_str() {
        "jan" => 1,
        "feb" => 2,
        "mar" => 3,
        "apr" => 4,
        "may" => 5,
        "jun" => 6,
        "jul" => 7,
        "aug" => 8,
        "sep" => 9,
        "oct" => 10,
        "nov" => 11,
        "dec" => 12,
        _ => 1,
    };
    let year: u32 = parts[2].parse().unwrap_or(2000);
    let time = parts[3];
    let tz = parts[4];

    let tp: Vec<&str> = time.split(':').collect();
    let hour: u32 = tp.first().and_then(|s| s.parse().ok()).unwrap_or(0);
    let min: u32 = tp.get(1).and_then(|s| s.parse().ok()).unwrap_or(0);
    let sec: u32 = tp.get(2).and_then(|s| s.parse().ok()).unwrap_or(0);

    // issue #45(d): apply the numeric zone offset to yield a true UTC instant
    // instead of dropping it (an `+0200` header was rendered 2h off, an `-0700`
    // header 7h off). A recognized offset → convert to UTC and emit `...Z`; an
    // unrecognized alpha zone (obsolete EST/PDT/… or a bogus token) → keep the
    // prior best-effort naive string (no suffix), never invent an offset.
    if let Some(offset_secs) = rfc2822_offset_seconds(tz) {
        if let Some(iso) = local_offset_to_utc_iso(year, month, day, hour, min, sec, offset_secs) {
            return iso;
        }
    }

    format!(
        "{:04}-{:02}-{:02}T{:02}:{:02}:{:02}",
        year, month, day, hour, min, sec
    )
}

/// Parse an RFC 2822 zone token into an offset in **seconds east of UTC**.
/// Handles `+HHMM` / `-HHMM` / `+HH:MM` / `-HH:MM` and the named UTC aliases
/// `UT` / `UTC` / `GMT` / `Z`. Obsolete alpha zones (EST, PDT, …) are treated as
/// unknown (RFC 2822 maps them to `-0000`, "offset unknown") and return `None`
/// so the caller keeps the naive best-effort rather than guessing.
fn rfc2822_offset_seconds(tz: &str) -> Option<i32> {
    match tz {
        "UT" | "UTC" | "GMT" | "Z" => return Some(0),
        _ => {}
    }
    let bytes = tz.as_bytes();
    let sign: i32 = match bytes.first()? {
        b'+' => 1,
        b'-' => -1,
        _ => return None,
    };
    // Accept both `+HHMM` and `+HH:MM`; keep only the digits after the sign.
    let digits: String = tz[1..].chars().filter(|c| c.is_ascii_digit()).collect();
    if digits.len() != 4 {
        return None;
    }
    let hh: i32 = digits[0..2].parse().ok()?;
    let mm: i32 = digits[2..4].parse().ok()?;
    if hh > 23 || mm > 59 {
        return None;
    }
    Some(sign * (hh * 3600 + mm * 60))
}

/// Build a UTC ISO 8601 string from local calendar fields plus a fixed offset
/// (seconds east of UTC). Uses chrono so a day/month/year rollover across the
/// offset is handled correctly. Returns `None` on an out-of-range field.
fn local_offset_to_utc_iso(
    year: u32,
    month: u32,
    day: u32,
    hour: u32,
    min: u32,
    sec: u32,
    offset_secs: i32,
) -> Option<String> {
    let offset = FixedOffset::east_opt(offset_secs)?;
    let naive = NaiveDate::from_ymd_opt(year as i32, month, day)?.and_hms_opt(hour, min, sec)?;
    let dt = offset.from_local_datetime(&naive).single()?;
    Some(
        dt.with_timezone(&Utc)
            .format("%Y-%m-%dT%H:%M:%SZ")
            .to_string(),
    )
}

/// Compute the IMAP sequence-number range for the `count` most-recent messages
/// in a mailbox whose EXISTS count is `total`.
///
/// EXISTS is the highest sequence number, so the newest `count` messages are
/// `[total - count + 1 .. total]`, clamped so the low end never drops below 1
/// (a mailbox with fewer than `count` messages just yields all of them, never
/// an inverted `high:low` range, which servers reject and which silently
/// returned zero messages before issue-45). Returns `None` when there is
/// nothing to fetch (`total == 0` or `count == 0`) so the caller skips the
/// FETCH entirely instead of issuing a malformed one. A single-message range
/// collapses to `"N"` rather than `"N:N"`. Pure + testable.
fn recent_seq_range(total: u32, count: u32) -> Option<String> {
    if total == 0 || count == 0 {
        return None;
    }
    let start = total.saturating_sub(count.saturating_sub(1)).max(1);
    if start >= total {
        Some(total.to_string())
    } else {
        Some(format!("{}:{}", start, total))
    }
}

/// Synchronous IMAP fetch, runs inside spawn_blocking.
// IMAP connection + selection parameters; inherent to the protocol call.
#[allow(clippy::too_many_arguments)]
fn fetch_imap_messages_sync(
    host: String,
    port: u16,
    username: String,
    password: String,
    folder: String,
    count: u32,
    account_id: String,
    account_email: String,
    account_nickname: String,
    account_provider: String,
) -> Result<Vec<EmailMessage>, String> {
    // Re-validate the stored host before reconnecting (DNS-rebinding defense: the
    // host validated at add-time could now resolve to a private address). Honors
    // the global bypass. Already inside spawn_blocking, so the DNS lookup is fine.
    widget_netguard::check_host_port(&host, port)?;

    let client = imap_connect(&host, port)?;

    let mut session = client
        .login(&username, &password)
        .map_err(|(e, _)| format!("IMAP login failed: {}", e))?;

    let messages = fetch_imap_messages_from_session(
        &mut session,
        &folder,
        count,
        &account_id,
        &account_email,
        &account_nickname,
        &account_provider,
    );

    session.logout().ok();
    messages
}

/// Longest preview handed to the frontend, in chars.
const PREVIEW_MAX: usize = 200;
/// How much of a single MIME part is decoded. A preview needs 200 chars; the
/// rest of a body (an inline image, a long newsletter) is never worth decoding.
const PART_SCAN_LIMIT: usize = 16 * 1024;
/// Depth/width caps on the MIME walk so a pathological (or hostile) structure
/// can never spin the fetch thread.
const MIME_MAX_DEPTH: usize = 6;
const MIME_MAX_PARTS: usize = 32;

/// Build a short plaintext preview from a message's headers plus its raw IMAP
/// body (`BODY[TEXT]`).
///
/// issue #114: `BODY[TEXT]` of a multipart message is the *whole* MIME body —
/// the "This is a multi-part message in MIME format." preamble, the boundary
/// delimiters and every part's own headers. Stripping tags out of that (the
/// original #45b implementation) previewed `Content-Type: text/plain;
/// charset=utf-8` instead of a single word the sender wrote. So the body is
/// walked as MIME: the first `text/plain` part wins, `text/html` is the
/// fallback, and the chosen part is decoded from its transfer encoding and
/// charset before being collapsed to one line and truncated on a char boundary.
///
/// Still best-effort, not a mail renderer: attachments are skipped rather than
/// summarized, non-text leaves are ignored, and a structure that declares a
/// boundary it never uses yields an empty preview rather than boundary soup.
fn build_imap_preview(headers: &str, raw_body: &str) -> String {
    let ctype = parse_header_field_raw(headers, "content-type");
    let cte = parse_header_field_raw(headers, "content-transfer-encoding");
    let disposition = parse_header_field_raw(headers, "content-disposition");

    let mut parts: Vec<(bool, String)> = Vec::new();
    collect_mime_text(&ctype, &cte, &disposition, raw_body, 0, &mut parts);

    // text/plain is what the sender wrote for exactly this purpose; the HTML
    // alternative is the same words wrapped in markup, so it is the fallback.
    let chosen = parts
        .iter()
        .find(|(is_html, _)| !is_html)
        .or_else(|| parts.first());

    let text = match chosen {
        Some((true, body)) => strip_html(body),
        Some((false, body)) => body.clone(),
        None => String::new(),
    };

    let collapsed = text.split_whitespace().collect::<Vec<_>>().join(" ");
    if collapsed.chars().count() > PREVIEW_MAX {
        collapsed.chars().take(PREVIEW_MAX).collect()
    } else {
        collapsed
    }
}

/// Walk a MIME entity, appending every decoded text leaf as `(is_html, text)`
/// in document order. `ctype` / `cte` / `disposition` are this entity's own
/// (raw, undecoded) header values; an empty `ctype` means the entity declared
/// none, which per RFC 2045 §5.2 defaults to `text/plain`.
fn collect_mime_text(
    ctype: &str,
    cte: &str,
    disposition: &str,
    body: &str,
    depth: usize,
    out: &mut Vec<(bool, String)>,
) {
    if depth > MIME_MAX_DEPTH || out.len() >= MIME_MAX_PARTS {
        return;
    }
    let mime = mime_type(ctype);

    if mime.starts_with("multipart/") {
        // No boundary = nothing can be split out of the body, and the body is
        // delimiter soup, so previewing it raw is worse than previewing nothing.
        let Some(boundary) = mime_param(ctype, "boundary") else {
            return;
        };
        for part in split_mime_parts(body, &boundary) {
            let (part_headers, part_body) = split_part_headers(part);
            collect_mime_text(
                &parse_header_field_raw(part_headers, "content-type"),
                &parse_header_field_raw(part_headers, "content-transfer-encoding"),
                &parse_header_field_raw(part_headers, "content-disposition"),
                part_body,
                depth + 1,
                out,
            );
        }
        return;
    }

    // A forwarded message carries its own header block; recurse into it so a
    // mail whose only text lives in the attached original still previews.
    if mime == "message/rfc822" {
        let (inner_headers, inner_body) = split_part_headers(body);
        collect_mime_text(
            &parse_header_field_raw(inner_headers, "content-type"),
            &parse_header_field_raw(inner_headers, "content-transfer-encoding"),
            &parse_header_field_raw(inner_headers, "content-disposition"),
            inner_body,
            depth + 1,
            out,
        );
        return;
    }

    // An attachment is never the message's own text, even when it is text/*.
    if disposition.trim_start().to_ascii_lowercase().starts_with("attachment") {
        return;
    }
    if !mime.starts_with("text/") {
        return; // image/*, application/*: nothing readable to preview.
    }

    let charset = mime_param(ctype, "charset").unwrap_or_default();
    let raw = truncate_chars(body, PART_SCAN_LIMIT);
    let bytes = match cte.trim().to_ascii_lowercase().as_str() {
        "quoted-printable" => decode_qp_body_bytes(raw),
        // A body that does not decode is corrupt; showing the base64 itself
        // would be the #114 bug in another costume, so drop the part.
        "base64" => match base64_decode_bytes(raw) {
            Some(b) => b,
            None => return,
        },
        _ => raw.as_bytes().to_vec(),
    };

    // An entity that declared no Content-Type gets the tag strip: it defaults
    // to text/plain, but senders that omit the header are exactly the ones who
    // put raw HTML in the body. A declared text/plain is trusted verbatim, so
    // a plain-text `<https://link>` survives instead of being eaten as a tag.
    let is_html = mime == "text/html" || ctype.trim().is_empty();
    out.push((is_html, decode_charset_bytes(&bytes, &charset)));
}

/// Media type of a `Content-Type` value, lowercased and stripped of parameters.
/// An absent header defaults to `text/plain` (RFC 2045 §5.2).
fn mime_type(ctype: &str) -> String {
    let head = ctype.split(';').next().unwrap_or("").trim();
    if head.is_empty() {
        "text/plain".to_string()
    } else {
        head.to_ascii_lowercase()
    }
}

/// Read one parameter out of a structured header value (`; name=value`, value
/// optionally quoted). Quote-aware, because a boundary may legally contain `;`.
fn mime_param(value: &str, name: &str) -> Option<String> {
    let mut segments: Vec<String> = Vec::new();
    let mut current = String::new();
    let mut in_quotes = false;
    for ch in value.chars() {
        match ch {
            '"' => {
                in_quotes = !in_quotes;
                current.push(ch);
            }
            ';' if !in_quotes => segments.push(std::mem::take(&mut current)),
            _ => current.push(ch),
        }
    }
    segments.push(current);

    for seg in segments.iter().skip(1) {
        let Some((key, raw)) = seg.split_once('=') else {
            continue;
        };
        if !key.trim().eq_ignore_ascii_case(name) {
            continue;
        }
        let val = raw.trim();
        let val = val
            .strip_prefix('"')
            .and_then(|v| v.strip_suffix('"'))
            .unwrap_or(val);
        if !val.is_empty() {
            return Some(val.to_string());
        }
    }
    None
}

/// Split a multipart body on its boundary delimiters (RFC 2046 §5.1.1),
/// returning each part's raw bytes (headers included). The preamble before the
/// first delimiter and the epilogue after the closing one are dropped — the
/// preamble is precisely the "This is a multi-part message…" line of #114.
fn split_mime_parts<'a>(body: &'a str, boundary: &str) -> Vec<&'a str> {
    let delim = format!("--{}", boundary);
    let close = format!("--{}--", boundary);
    let mut parts: Vec<&str> = Vec::new();
    let mut open: Option<usize> = None;
    let mut pos = 0usize;

    for line in body.split_inclusive('\n') {
        let line_start = pos;
        pos += line.len();
        // Trailing whitespace after a delimiter is permitted and common.
        let t = line.trim_end_matches(['\r', '\n', ' ', '\t']);
        if t != delim && t != close {
            continue;
        }
        if let Some(start) = open.take() {
            parts.push(&body[start..line_start]);
        }
        if t == close || parts.len() >= MIME_MAX_PARTS {
            return parts;
        }
        open = Some(pos);
    }

    // Unterminated final part (a truncated fetch): keep what arrived.
    if let Some(start) = open {
        if start < body.len() {
            parts.push(&body[start..]);
        }
    }
    parts
}

/// Split a MIME entity into its header block and body at the first empty line.
/// An entity whose first line is not a header (malformed, or a body that was
/// handed over without headers) is treated as all body.
fn split_part_headers(part: &str) -> (&str, &str) {
    let first = part.lines().next().unwrap_or("");
    if !first.is_empty() && !looks_like_header_line(first) {
        return ("", part);
    }
    let mut pos = 0usize;
    for line in part.split_inclusive('\n') {
        if line.trim_end_matches(['\r', '\n']).is_empty() {
            return (&part[..pos], &part[pos + line.len()..]);
        }
        pos += line.len();
    }
    (part, "")
}

/// `Name: value` — a non-empty field name with no whitespace in it.
fn looks_like_header_line(line: &str) -> bool {
    match line.split_once(':') {
        Some((name, _)) => !name.is_empty() && !name.contains(char::is_whitespace),
        None => false,
    }
}

/// Decode part bytes to text. UTF-8 when the bytes are valid UTF-8 (which
/// covers us-ascii and any correctly-labelled UTF-8 body); otherwise a
/// Latin-1-family charset is decoded through the Windows-1252 table, since a
/// mail labelled `iso-8859-1` that uses 0x80–0x9F is really cp1252 in practice.
/// Anything else degrades to lossy UTF-8 rather than failing the preview.
fn decode_charset_bytes(bytes: &[u8], charset: &str) -> String {
    if let Ok(s) = std::str::from_utf8(bytes) {
        return s.to_string();
    }
    let cs = charset.trim().to_ascii_lowercase();
    let latin1 = cs.starts_with("iso-8859-1")
        || cs.starts_with("iso8859-1")
        || cs.starts_with("windows-1252")
        || cs.starts_with("cp1252")
        || cs == "latin1";
    if !latin1 {
        return String::from_utf8_lossy(bytes).into_owned();
    }
    // Windows-1252 differs from Latin-1 only in 0x80–0x9F.
    const HIGH: [char; 32] = [
        '\u{20AC}', '\u{0081}', '\u{201A}', '\u{0192}', '\u{201E}', '\u{2026}', '\u{2020}',
        '\u{2021}', '\u{02C6}', '\u{2030}', '\u{0160}', '\u{2039}', '\u{0152}', '\u{008D}',
        '\u{017D}', '\u{008F}', '\u{0090}', '\u{2018}', '\u{2019}', '\u{201C}', '\u{201D}',
        '\u{2022}', '\u{2013}', '\u{2014}', '\u{02DC}', '\u{2122}', '\u{0161}', '\u{203A}',
        '\u{0153}', '\u{009D}', '\u{017E}', '\u{0178}',
    ];
    bytes
        .iter()
        .map(|&b| match b {
            0x80..=0x9F => HIGH[(b - 0x80) as usize],
            _ => b as char,
        })
        .collect()
}

/// Truncate a `&str` to at most `max` chars, on a char boundary.
fn truncate_chars(s: &str, max: usize) -> &str {
    match s.char_indices().nth(max) {
        Some((idx, _)) => &s[..idx],
        None => s,
    }
}

/// Reduce an HTML body to its readable text: `<script>` / `<style>` blocks are
/// dropped whole (their contents are code, not prose — previewing a stylesheet
/// was the other half of what #114 reported), tags are removed, and the handful
/// of entities that survive that treatment are decoded.
fn strip_html(raw: &str) -> String {
    let bytes = raw.as_bytes();
    let mut out = String::with_capacity(raw.len().min(PART_SCAN_LIMIT));
    let mut i = 0usize;

    while i < bytes.len() {
        if bytes[i] != b'<' {
            let start = i;
            while i < bytes.len() && bytes[i] != b'<' {
                i += 1;
            }
            out.push_str(&raw[start..i]);
            continue;
        }
        // Inside a tag: find its name, then skip to the tag's end.
        let rest = &raw[i..];
        let lower_name: String = rest[1..]
            .chars()
            .take_while(|c| c.is_ascii_alphanumeric())
            .collect::<String>()
            .to_ascii_lowercase();
        let tag_end = rest.find('>').map(|p| i + p + 1).unwrap_or(bytes.len());

        if lower_name == "script" || lower_name == "style" {
            let closer = format!("</{}", lower_name);
            let after = &raw[tag_end..];
            let skip_to = after
                .to_ascii_lowercase()
                .find(&closer)
                .map(|p| tag_end + p)
                .unwrap_or(bytes.len());
            // Jump past the closing tag as well.
            i = raw[skip_to..]
                .find('>')
                .map(|p| skip_to + p + 1)
                .unwrap_or(bytes.len());
            continue;
        }
        i = tag_end;
    }

    decode_html_entities(&out)
}

/// Decode the named entities that actually show up in mail text, plus numeric
/// references. An unknown entity is left verbatim (better a literal `&foo;`
/// than a swallowed word).
fn decode_html_entities(s: &str) -> String {
    if !s.contains('&') {
        return s.to_string();
    }
    let mut out = String::with_capacity(s.len());
    let mut rest = s;

    while let Some(amp) = rest.find('&') {
        out.push_str(&rest[..amp]);
        rest = &rest[amp..];
        // An entity is short; anything longer is a literal ampersand.
        let end = rest[..rest.len().min(12)].find(';');
        let Some(end) = end else {
            out.push('&');
            rest = &rest[1..];
            continue;
        };
        let name = &rest[1..end];
        let decoded = match name.to_ascii_lowercase().as_str() {
            "nbsp" => Some(' '),
            "amp" => Some('&'),
            "lt" => Some('<'),
            "gt" => Some('>'),
            "quot" => Some('"'),
            "apos" | "#39" => Some('\''),
            _ => name
                .strip_prefix('#')
                .and_then(|n| match n.strip_prefix(['x', 'X']) {
                    Some(hex) => u32::from_str_radix(hex, 16).ok(),
                    None => n.parse::<u32>().ok(),
                })
                .and_then(char::from_u32),
        };
        match decoded {
            Some(c) => out.push(c),
            None => out.push_str(&rest[..=end]),
        }
        rest = &rest[end + 1..];
    }

    out.push_str(rest);
    out
}

/// Post-login IMAP fetch logic, generic over the underlying stream so the same
/// session/select/fetch/parse path runs against both production's TLS stream and
/// a plaintext `TcpStream` in tests. `fetch_imap_messages_sync` calls this after
/// `imap::connect` + `.login()`; behavior here is byte-for-byte what used to live
/// inline in that function.
// account_* params mirror EmailMessage fields populated per message.
#[allow(clippy::too_many_arguments)]
fn fetch_imap_messages_from_session<S: std::io::Read + std::io::Write>(
    session: &mut imap::Session<S>,
    folder: &str,
    count: u32,
    account_id: &str,
    account_email: &str,
    account_nickname: &str,
    account_provider: &str,
) -> Result<Vec<EmailMessage>, String> {
    let folder_name = resolve_imap_mailbox(session, folder);
    let mailbox = session
        .select(&folder_name)
        .map_err(|e| format!("Cannot select folder '{}': {}", folder_name, e))?;

    let total = mailbox.exists;
    // Capture the mailbox incarnation so each id can be validated at mutation
    // time (mark/delete/move). If the server omits UIDVALIDITY the id stays the
    // two-part shape and the guard is skipped (never worse than before).
    let uid_validity = mailbox.uid_validity;
    let mut messages: Vec<EmailMessage> = Vec::new();

    if let Some(range) = recent_seq_range(total, count) {
        // The att list MUST be parenthesized: the `imap` crate sends the query
        // verbatim (`FETCH <range> <query>`), and RFC 3501 only permits a bare
        // att when there is exactly one. A strict server (Dovecot) rejects the
        // unparenthesized multi-att form with "Invalid arguments" (issue #45);
        // Gmail/Outlook happen to accept it, which masked the bug.
        //
        // issue #45(b): also pull the text body (`BODY.PEEK[TEXT]`, PEEK so the
        // fetch does not set \Seen) to populate a short preview — previously
        // hardcoded empty for IMAP. Lightweight best-effort: a plaintext snippet,
        // no full MIME decode.
        let fetches = session
            .fetch(&range, "(RFC822.HEADER FLAGS UID BODY.PEEK[TEXT])")
            .map_err(|e| format!("IMAP fetch error: {}", e))?;

        for msg in fetches.iter() {
            let raw = msg.header().unwrap_or(&[]);
            // Lossy, not from_utf8().unwrap_or(""): RFC 5322 headers routinely
            // carry raw 8-bit bytes (legacy ISO-8859-1 / Windows-1252 display
            // names + subjects). A strict decode fails on the first such byte and
            // collapsed the WHOLE header to "", blanking subject/from/date for the
            // message. Lossy degrades only those bytes; RFC 2047 encoded-words
            // (pure ASCII) still decode fully downstream.
            let hdr = String::from_utf8_lossy(raw);

            let from_raw = parse_header_field(&hdr, "from");
            let subject = parse_header_field(&hdr, "subject");
            let date_raw = parse_header_field(&hdr, "date");

            let (from_name, from_email) = parse_from_field(&from_raw);
            let received_at = parse_rfc2822_date(&date_raw);
            let is_read = msg
                .flags()
                .iter()
                .any(|f| matches!(f, imap::types::Flag::Seen));
            let uid = msg.uid.unwrap_or(0);
            // issue #45(b): derive a short preview from BODY[TEXT] when present.
            // `None` = no text part in the response → empty preview (correct
            // fallback, not an error). issue #114: the headers go in too — the
            // top-level Content-Type carries the boundary that turns the raw
            // multipart body into parts to choose from.
            let preview = match msg.text() {
                Some(bytes) => build_imap_preview(&hdr, &String::from_utf8_lossy(bytes)),
                None => String::new(),
            };

            messages.push(EmailMessage {
                // Encode the source folder AND mailbox incarnation
                // (`folder|uid|uidvalidity`) so mark/delete/move target the
                // mailbox the message was displayed in, not a hardcoded INBOX,
                // and refuse if that mailbox was recreated under it.
                id: match uid_validity {
                    Some(v) => format!("{}:{}|{}|{}", account_id, folder, uid, v),
                    None => format!("{}:{}|{}", account_id, folder, uid),
                },
                subject: if subject.is_empty() {
                    "(no subject)".into()
                } else {
                    subject
                },
                from_name,
                from_email,
                received_at,
                is_read,
                preview,
                web_link: String::new(),
                account_id: account_id.to_string(),
                account_email: account_email.to_string(),
                account_nickname: account_nickname.to_string(),
                account_provider: account_provider.to_string(),
            });
        }

        messages.reverse(); // Newest first
    }

    Ok(messages)
}

/// Synchronous IMAP STATUS for folder counts (S-25), runs inside spawn_blocking.
/// Returns total + unread for the resolved mailbox. Errors propagate (they are
/// logged + caught in the widget's poll loop) instead of being silently zeroed.
fn imap_folder_status_sync(
    host: String,
    port: u16,
    username: String,
    password: String,
    folder: String,
) -> Result<FolderStatus, String> {
    widget_netguard::check_host_port(&host, port)?;

    let client = imap_connect(&host, port)?;
    let mut session = client
        .login(&username, &password)
        .map_err(|(e, _)| format!("IMAP login failed: {}", e))?;

    let result = imap_folder_status_in_session(&mut session, &folder);
    session.logout().ok();
    result
}

fn imap_folder_status_in_session<S: std::io::Read + std::io::Write>(
    session: &mut imap::Session<S>,
    folder: &str,
) -> Result<FolderStatus, String> {
    let mailbox = resolve_imap_mailbox(session, folder);
    // The att list MUST be parenthesized (RFC 3501 status grammar); a strict
    // server rejects a bare list, the same class of bug as issue #45's FETCH.
    session
        .status(&mailbox, "(MESSAGES UNSEEN)")
        .map_err(|e| format!("IMAP STATUS failed for '{}': {}", mailbox, e))?;

    // rust-imap 2.x delivers the STATUS counts as an UNSOLICITED response, NOT in
    // the `Mailbox` returned by `.status()` (that stays default → exists 0, unseen
    // None). Reading `status.exists`/`status.unseen` therefore reported 0/0 for
    // EVERY IMAP folder. Drain the unsolicited channel for the matching STATUS
    // datum and read MESSAGES / UNSEEN from there.
    let mut total = 0i64;
    let mut unread = 0i64;
    for resp in session.unsolicited_responses.try_iter() {
        if let imap::types::UnsolicitedResponse::Status { attributes, .. } = resp {
            for att in attributes {
                match att {
                    imap::types::StatusAttribute::Messages(n) => total = n as i64,
                    imap::types::StatusAttribute::Unseen(n) => unread = n as i64,
                    _ => {}
                }
            }
        }
    }
    Ok(FolderStatus { unread, total })
}

// ---------------------------------------------------------------------------
// Microsoft Graph – email
// ---------------------------------------------------------------------------

#[derive(Deserialize)]
struct GraphResponse {
    value: Vec<GraphMessage>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphMessage {
    id: String,
    subject: Option<String>,
    from: Option<GraphRecipient>,
    received_date_time: String,
    is_read: bool,
    body_preview: String,
    web_link: String,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphRecipient {
    email_address: GraphEmailAddress,
}

#[derive(Deserialize)]
struct GraphEmailAddress {
    name: Option<String>,
    address: Option<String>,
}

/// Map our internal folder names (Microsoft-flavored) to Gmail label IDs.
fn gmail_label_id(folder: &str) -> &'static str {
    match folder {
        "inbox" => "INBOX",
        "sent" | "sentitems" => "SENT",
        "drafts" => "DRAFT",
        "deleteditems" => "TRASH",
        "junkemail" => "SPAM",
        _ => "INBOX",
    }
}

/// Fetch Gmail messages for a Google account and map them onto the shared
/// `EmailMessage` shape. Uses `list` + `get(format=metadata)` to pull headers
/// and snippet without downloading full bodies.
async fn fetch_gmail_messages(
    app: &tauri::AppHandle,
    account_id: &str,
    account_email: &str,
    account_nickname: &str,
    account_provider: &str,
    folder: &str,
    count: u32,
) -> Result<Vec<EmailMessage>, String> {
    let token = get_valid_token(app, account_id).await?;
    let label = gmail_label_id(folder);
    let client = http_client();

    let list_url = format!(
        "https://gmail.googleapis.com/gmail/v1/users/me/messages?labelIds={label}&maxResults={count}"
    );
    let list_resp = client
        .get(&list_url)
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !list_resp.status().is_success() {
        let s = list_resp.status().as_u16();
        if s == 401 {
            // Keep the account: a 401 is usually just an expired token, not a
            // reason to delete the user's account + stored credentials. (N2)
            return Err("Session expired, please reconnect this account".into());
        }
        return Err(format!("Gmail API error {s}"));
    }

    #[derive(Deserialize)]
    struct GmailListItem {
        id: String,
    }
    #[derive(Deserialize)]
    struct GmailListResp {
        #[serde(default)]
        messages: Vec<GmailListItem>,
    }

    let list: GmailListResp = list_resp.json().await.map_err(|e| e.to_string())?;

    let mut out: Vec<EmailMessage> = Vec::with_capacity(list.messages.len());
    for m in list.messages {
        let get_url = format!(
            "https://gmail.googleapis.com/gmail/v1/users/me/messages/{}?format=metadata\
             &metadataHeaders=Subject&metadataHeaders=From&metadataHeaders=Date",
            m.id
        );
        let Ok(resp) = client.get(&get_url).bearer_auth(&token).send().await else {
            continue;
        };
        if !resp.status().is_success() {
            continue;
        }

        #[derive(Deserialize)]
        struct Header {
            name: String,
            value: String,
        }
        #[derive(Deserialize)]
        struct Payload {
            #[serde(default)]
            headers: Vec<Header>,
        }
        #[derive(Deserialize)]
        struct GmailMsg {
            id: String,
            #[serde(rename = "threadId")]
            thread_id: Option<String>,
            #[serde(default, rename = "labelIds")]
            label_ids: Vec<String>,
            snippet: Option<String>,
            #[serde(rename = "internalDate")]
            internal_date: Option<String>,
            payload: Option<Payload>,
        }

        let Ok(msg) = resp.json::<GmailMsg>().await else {
            continue;
        };

        let mut subject = String::from("(no subject)");
        let mut from_raw = String::new();
        if let Some(p) = &msg.payload {
            for h in &p.headers {
                match h.name.as_str() {
                    "Subject" => subject = h.value.clone(),
                    "From" => from_raw = h.value.clone(),
                    _ => {}
                }
            }
        }

        // Parse "Display Name <addr@host>" into name/email.
        let (from_name, from_email) = if let Some(open) = from_raw.find('<') {
            let name = from_raw[..open].trim().trim_matches('"').to_string();
            let addr = from_raw[open + 1..]
                .trim_end_matches('>')
                .trim()
                .to_string();
            (name, addr)
        } else {
            (String::new(), from_raw.trim().to_string())
        };

        // internalDate is ms since epoch.
        let received_at = msg
            .internal_date
            .as_deref()
            .and_then(|s| s.parse::<i64>().ok())
            .map(|ms| {
                let secs = ms / 1000;
                // Produce an ISO-8601 UTC string without pulling in chrono.
                format_iso8601_utc(secs)
            })
            .unwrap_or_default();

        let is_read = !msg.label_ids.iter().any(|l| l == "UNREAD");
        let web_link = msg
            .thread_id
            .as_deref()
            .map(|t| format!("https://mail.google.com/mail/u/0/#inbox/{t}"))
            .unwrap_or_default();

        out.push(EmailMessage {
            id: format!("{account_id}:{}", msg.id),
            subject,
            from_name,
            from_email,
            received_at,
            is_read,
            preview: msg.snippet.unwrap_or_default(),
            web_link,
            account_id: account_id.to_string(),
            account_email: account_email.to_string(),
            account_nickname: account_nickname.to_string(),
            account_provider: account_provider.to_string(),
        });
    }
    Ok(out)
}

/// Tiny ISO-8601 UTC formatter for `YYYY-MM-DDTHH:MM:SSZ` from epoch seconds.
fn format_iso8601_utc(secs: i64) -> String {
    // Days since 1970-01-01
    let days = secs.div_euclid(86400);
    let seconds_of_day = secs.rem_euclid(86400) as u32;
    let (h, rem) = (seconds_of_day / 3600, seconds_of_day % 3600);
    let (min, sec) = (rem / 60, rem % 60);

    // Civil-from-days (Howard Hinnant's algorithm).
    let z = days + 719468;
    let era = z.div_euclid(146097);
    let doe = z.rem_euclid(146097) as u64;
    let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
    let y = yoe as i64 + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let d = (doy - (153 * mp + 2) / 5 + 1) as u32;
    let m = if mp < 10 { mp + 3 } else { mp - 9 } as u32;
    let y = if m <= 2 { y + 1 } else { y };

    format!("{y:04}-{m:02}-{d:02}T{h:02}:{min:02}:{sec:02}Z")
}

async fn fetch_for_account(
    app: &tauri::AppHandle,
    account_id: &str,
    account_email: &str,
    account_nickname: &str,
    account_provider: &str,
    folder: &str,
    count: u32,
) -> Result<Vec<EmailMessage>, String> {
    // Google email fetch goes through Gmail API, not Graph.
    let accounts = load_accounts(app);
    if let Some(data) = accounts.get(account_id) {
        if data.provider == "google" {
            return fetch_gmail_messages(
                app,
                account_id,
                account_email,
                account_nickname,
                account_provider,
                folder,
                count,
            )
            .await;
        }
    }
    drop(accounts);

    let token = get_valid_token(app, account_id).await?;

    let url = format!(
        "https://graph.microsoft.com/v1.0/me/mailFolders/{}/messages\
        ?$top={}\
        &$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink\
        &$orderby=receivedDateTime%20desc",
        folder, count
    );

    let client = http_client();
    let resp = get_with_retry(&client, &url, &token, None)
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let status = resp.status().as_u16();
        if status == 401 {
            // Keep the account: a 401 is usually just an expired token, not a
            // reason to delete the user's account + stored credentials. (N2)
            return Err("Session expired, please reconnect this account".into());
        }
        return Err(format!("Graph API error {}", status));
    }

    let graph: GraphResponse = resp.json().await.map_err(|e| e.to_string())?;
    let account_id = account_id.to_string();
    let account_email = account_email.to_string();
    let account_nickname = account_nickname.to_string();
    let account_provider = account_provider.to_string();

    Ok(graph
        .value
        .into_iter()
        .map(|m| {
            let (from_name, from_email) = m
                .from
                .map(|r| {
                    (
                        r.email_address.name.unwrap_or_default(),
                        r.email_address.address.unwrap_or_default(),
                    )
                })
                .unwrap_or_default();

            EmailMessage {
                id: format!("{}:{}", account_id, m.id),
                subject: m.subject.unwrap_or_else(|| "(no subject)".into()),
                from_name,
                from_email,
                received_at: m.received_date_time,
                is_read: m.is_read,
                preview: m.body_preview,
                web_link: m.web_link,
                account_id: account_id.clone(),
                account_email: account_email.clone(),
                account_nickname: account_nickname.clone(),
                account_provider: account_provider.clone(),
            }
        })
        .collect())
}

pub async fn get_messages(
    app: &tauri::AppHandle,
    account_id: &str,
    folder: &str,
    count: u32,
) -> Result<Vec<EmailMessage>, String> {
    let accounts = load_accounts(app);

    if accounts.is_empty() {
        return Err("no_accounts".into());
    }

    let resolve_nick = |data: &AccountData| {
        data.nickname
            .clone()
            .unwrap_or_else(|| data.display_name.clone())
    };

    // Only email providers
    let targets: Vec<(String, AccountData)> = if account_id.is_empty() || account_id == "all" {
        accounts
            .into_iter()
            .filter(|(_, d)| {
                d.provider == "microsoft" || d.provider == "imap" || d.provider == "google"
            })
            .collect()
    } else {
        accounts
            .into_iter()
            .filter(|(id, d)| {
                id.as_str() == account_id
                    && (d.provider == "microsoft" || d.provider == "imap" || d.provider == "google")
            })
            .collect()
    };

    let target_count = targets.len();
    let mut all: Vec<EmailMessage> = Vec::new();
    let mut last_error: Option<String> = None;
    let mut failures = 0usize;
    for (id, data) in &targets {
        let nickname = resolve_nick(data);
        let result: Result<Vec<EmailMessage>, String> = match data.provider.as_str() {
            "imap" => {
                let host = data.host.clone().unwrap_or_default();
                let port = data.port.unwrap_or(993);
                let uname = data.username.clone().unwrap_or_default();
                let pass = data.password.clone().unwrap_or_default();
                let folder_s = folder.to_string();
                let id_c = id.clone();
                let email_c = data.email.clone();
                let nick_c = nickname.clone();
                let prov_c = data.provider.clone();
                // spawn_blocking can fail only if the task panics; surface that as
                // an error string for this account rather than `?`-ing out of the
                // whole fetch (one bad account shouldn't blank a merged feed).
                match tokio::task::spawn_blocking(move || {
                    fetch_imap_messages_sync(
                        host, port, uname, pass, folder_s, count, id_c, email_c, nick_c, prov_c,
                    )
                })
                .await
                {
                    Ok(r) => r,
                    Err(join_err) => Err(format!("fetch task failed: {}", join_err)),
                }
            }
            _ => {
                fetch_for_account(
                    app,
                    id,
                    &data.email,
                    &nickname,
                    &data.provider,
                    folder,
                    count,
                )
                .await
            }
        };
        match result {
            Ok(mut msgs) => all.append(&mut msgs),
            // Previously this `Err` was silently dropped (`if let Ok(..)`), so a
            // failed IMAP connect/login/SELECT/FETCH produced an empty feed with
            // NOTHING logged: the widget showed "No messages" and the user had no
            // signal. Now every per-account failure is logged with its provider +
            // id, and (below) a total wipeout is surfaced as an error so the
            // widget shows the real message instead of a misleading empty state.
            Err(e) => {
                failures += 1;
                log::error!(
                    target: "email:get_messages",
                    "fetch failed provider={} account={} folder={}: {}",
                    data.provider, id, folder, e
                );
                last_error = Some(e);
            }
        }
    }

    // If every configured account failed, surface the error so the widget renders
    // its error state (S-06) rather than a misleading "No messages" (an empty list
    // means "fetched OK, nothing there", not "fetch failed"). A partial failure
    // keeps the messages we did get; the failures are already logged above.
    if failures == target_count && target_count > 0 {
        return Err(last_error.unwrap_or_else(|| "Failed to fetch messages".into()));
    }

    all.sort_by(|a, b| b.received_at.cmp(&a.received_at));
    all.truncate(count as usize);
    Ok(all)
}

// ---------------------------------------------------------------------------
// Folder status (Microsoft only)
// ---------------------------------------------------------------------------

#[derive(Serialize, Deserialize)]
pub struct FolderStatus {
    pub unread: i64,
    pub total: i64,
}

pub async fn get_folder_status(
    app: &tauri::AppHandle,
    account_id: &str,
    folder: &str,
) -> Result<FolderStatus, String> {
    let accounts = load_accounts(app);
    let data = accounts
        .get(account_id)
        .cloned()
        .ok_or_else(|| "Account not found".to_string())?;
    let provider = data.provider.clone();
    drop(accounts);

    // IMAP has no OAuth token: it used to fall through to the Graph branch,
    // where get_valid_token returned "No refresh token; please sign in again"
    // and folder-status polling (S-25/S-26) failed forever, so IMAP unread/total
    // counts never updated. Use an IMAP STATUS call instead (MESSAGES + UNSEEN).
    if provider == "imap" {
        let host = data.host.clone().unwrap_or_default();
        let port = data.port.unwrap_or(993);
        let uname = data.username.clone().unwrap_or_default();
        let pass = data.password.clone().unwrap_or_default();
        let folder_s = folder.to_string();
        return tokio::task::spawn_blocking(move || {
            imap_folder_status_sync(host, port, uname, pass, folder_s)
        })
        .await
        .map_err(|e| format!("status task failed: {}", e))?;
    }

    let token = get_valid_token(app, account_id).await?;
    let client = http_client();

    if provider == "google" {
        let label = gmail_label_id(folder);
        let url = format!("https://gmail.googleapis.com/gmail/v1/users/me/labels/{label}");
        let resp = client
            .get(&url)
            .bearer_auth(&token)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            return Err(format!("Gmail API error {}", resp.status().as_u16()));
        }
        let json: serde_json::Value = resp.json().await.map_err(|e| e.to_string())?;
        return Ok(FolderStatus {
            unread: json["messagesUnread"].as_i64().unwrap_or(0),
            total: json["messagesTotal"].as_i64().unwrap_or(0),
        });
    }

    let url = format!(
        "https://graph.microsoft.com/v1.0/me/mailFolders/{}?$select=unreadItemCount,totalItemCount",
        folder
    );
    let resp = client
        .get(&url)
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        return Err(format!("API error {}", resp.status().as_u16()));
    }

    let json: serde_json::Value = resp.json().await.map_err(|e| e.to_string())?;
    Ok(FolderStatus {
        unread: json["unreadItemCount"].as_i64().unwrap_or(0),
        total: json["totalItemCount"].as_i64().unwrap_or(0),
    })
}

// ---------------------------------------------------------------------------
// Message actions (mark read / unread / delete / move)
// ---------------------------------------------------------------------------

/// Split a composite message ID `accountId:rawId` into (accountId, rawId).
///
/// Uses `rsplit_once` (the LAST ':'), not the first: account ids themselves
/// contain colons for several providers (`google:{sub}`, `imap:user@host`), so
/// splitting on the first ':' returned a wrong account id and the lookup failed
/// ("Account not found"), silently breaking mark/delete/move for Google and
/// IMAP accounts. The `rawId` (provider message id / `folder|uid`) never
/// contains a ':', so the last ':' is unambiguously the separator.
fn split_message_id(message_id: &str) -> Result<(&str, &str), String> {
    message_id
        .rsplit_once(':')
        .ok_or_else(|| "Invalid message ID".to_string())
}

/// Parse an IMAP message `rawId` into (logical folder, UID, UIDVALIDITY?).
///
/// Ids encode `folder|uid|uidvalidity`. The source folder makes an action target
/// the mailbox the message was displayed in, not a hardcoded INBOX (which for
/// delete could expunge the wrong message). The trailing UIDVALIDITY (RFC 3501)
/// pins the mailbox *incarnation*: if the mailbox is deleted+recreated its
/// UIDVALIDITY bumps and the SAME UID then names a DIFFERENT message, so a
/// mutation refuses instead of hitting the wrong one (`ensure_uid_validity`).
///
/// Older id shapes stay supported so a mid-cycle upgrade never breaks: `folder|uid`
/// (no validity → guard skipped, never worse than before) and a bare `uid` with
/// no `|` (pre-folder legacy, defaults to "inbox"). A fresh poll re-mints the
/// full three-part id.
fn imap_folder_uid_validity(raw_id: &str) -> Result<(&str, u32, Option<u32>), String> {
    let mut parts = raw_id.splitn(3, '|');
    let (folder, uid_str, validity_str) = match (parts.next(), parts.next(), parts.next()) {
        (Some(f), Some(u), Some(v)) => (f, u, Some(v)),
        (Some(f), Some(u), None) => (f, u, None),
        (Some(u), None, None) => ("inbox", u, None),
        _ => return Err("Invalid message ID".to_string()),
    };
    let uid: u32 = uid_str.parse().map_err(|_| "Invalid UID".to_string())?;
    let uid_validity = match validity_str {
        Some(s) => Some(
            s.parse::<u32>()
                .map_err(|_| "Invalid UIDVALIDITY".to_string())?,
        ),
        None => None,
    };
    Ok((folder, uid, uid_validity))
}

/// Refuse a UID-targeted mutation when the mailbox's UIDVALIDITY no longer matches
/// the value captured when the message id was minted. A changed UIDVALIDITY means
/// the mailbox was deleted+recreated, so the SAME UID now names a DIFFERENT
/// message; proceeding would mark/delete/move the wrong one — silent data loss for
/// delete. Fires only when BOTH values are known and differ: an absent server
/// value can't be verified, so we proceed (never a regression vs. the old code,
/// which never checked at all).
fn ensure_uid_validity(
    mailbox: &imap::types::Mailbox,
    expected: Option<u32>,
    action: &str,
) -> Result<(), String> {
    if let (Some(exp), Some(actual)) = (expected, mailbox.uid_validity) {
        if exp != actual {
            return Err(format!(
                "Mailbox changed (UIDVALIDITY {} → {}); refusing to {} a possibly-different \
                 message. Refresh and try again.",
                exp, actual, action
            ));
        }
    }
    Ok(())
}

pub async fn mark_message_read(
    app: &tauri::AppHandle,
    message_id: &str,
    is_read: bool,
) -> Result<(), String> {
    let (account_id, raw_id) = split_message_id(message_id)?;
    let accounts = load_accounts(app);
    let data = accounts.get(account_id).ok_or("Account not found")?;

    match data.provider.as_str() {
        "imap" => {
            let host = data.host.clone().unwrap_or_default();
            let port = data.port.unwrap_or(993);
            let uname = data.username.clone().unwrap_or_default();
            let pass = data.password.clone().unwrap_or_default();
            let (folder, uid, uid_validity) = imap_folder_uid_validity(raw_id)?;
            let folder = folder.to_string();
            let read = is_read;
            tokio::task::spawn_blocking(move || {
                imap_set_flag_sync(
                    host,
                    port,
                    uname,
                    pass,
                    &folder,
                    uid,
                    "\\Seen",
                    read,
                    uid_validity,
                )
            })
            .await
            .map_err(|e| e.to_string())??;
        }
        "google" => {
            let token = get_valid_token(app, account_id).await?;
            let url = format!(
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/{}/modify",
                raw_id
            );
            let body = if is_read {
                serde_json::json!({ "removeLabelIds": ["UNREAD"] })
            } else {
                serde_json::json!({ "addLabelIds": ["UNREAD"] })
            };
            let client = http_client();
            let resp = client
                .post(&url)
                .bearer_auth(&token)
                .json(&body)
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("Gmail API error {}", resp.status().as_u16()));
            }
        }
        _ => {
            let token = get_valid_token(app, account_id).await?;
            let url = format!("https://graph.microsoft.com/v1.0/me/messages/{}", raw_id);
            let client = http_client();
            let resp = client
                .patch(&url)
                .bearer_auth(&token)
                .json(&serde_json::json!({ "isRead": is_read }))
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("API error {}", resp.status().as_u16()));
            }
        }
    }
    Ok(())
}

pub async fn delete_message(app: &tauri::AppHandle, message_id: &str) -> Result<(), String> {
    let (account_id, raw_id) = split_message_id(message_id)?;
    let accounts = load_accounts(app);
    let data = accounts.get(account_id).ok_or("Account not found")?;

    match data.provider.as_str() {
        "imap" => {
            let host = data.host.clone().unwrap_or_default();
            let port = data.port.unwrap_or(993);
            let uname = data.username.clone().unwrap_or_default();
            let pass = data.password.clone().unwrap_or_default();
            let (folder, uid, uid_validity) = imap_folder_uid_validity(raw_id)?;
            let folder = folder.to_string();
            tokio::task::spawn_blocking(move || {
                imap_delete_message_sync(host, port, uname, pass, &folder, uid, uid_validity)
            })
            .await
            .map_err(|e| e.to_string())??;
        }
        "google" => {
            // Gmail: move to Trash (reversible) rather than hard-delete.
            let token = get_valid_token(app, account_id).await?;
            let url = format!(
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/{}/trash",
                raw_id
            );
            let client = http_client();
            let resp = client
                .post(&url)
                .bearer_auth(&token)
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("Gmail API error {}", resp.status().as_u16()));
            }
        }
        _ => {
            let token = get_valid_token(app, account_id).await?;
            let url = format!("https://graph.microsoft.com/v1.0/me/messages/{}", raw_id);
            let client = http_client();
            let resp = client
                .delete(&url)
                .bearer_auth(&token)
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("API error {}", resp.status().as_u16()));
            }
        }
    }
    Ok(())
}

pub async fn move_message(
    app: &tauri::AppHandle,
    message_id: &str,
    destination_folder: &str,
) -> Result<(), String> {
    let (account_id, raw_id) = split_message_id(message_id)?;
    let accounts = load_accounts(app);
    let data = accounts.get(account_id).ok_or("Account not found")?;

    match data.provider.as_str() {
        "imap" => {
            let host = data.host.clone().unwrap_or_default();
            let port = data.port.unwrap_or(993);
            let uname = data.username.clone().unwrap_or_default();
            let pass = data.password.clone().unwrap_or_default();
            let (folder, uid, uid_validity) = imap_folder_uid_validity(raw_id)?;
            let folder = folder.to_string();
            let dest = destination_folder.to_string();
            tokio::task::spawn_blocking(move || {
                imap_move_message_sync(host, port, uname, pass, &folder, uid, &dest, uid_validity)
            })
            .await
            .map_err(|e| e.to_string())??;
        }
        "google" => {
            // Gmail "move" = relabel: remove current system labels, add destination.
            let token = get_valid_token(app, account_id).await?;
            let dest_label = gmail_label_id(destination_folder);
            // Known system labels we may want to clear when moving between folders.
            let mut remove: Vec<&str> = vec!["INBOX", "TRASH", "SPAM"];
            remove.retain(|l| *l != dest_label);
            let url = format!(
                "https://gmail.googleapis.com/gmail/v1/users/me/messages/{}/modify",
                raw_id
            );
            let body = serde_json::json!({
                "addLabelIds": [dest_label],
                "removeLabelIds": remove,
            });
            let client = http_client();
            let resp = client
                .post(&url)
                .bearer_auth(&token)
                .json(&body)
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("Gmail API error {}", resp.status().as_u16()));
            }
        }
        _ => {
            let token = get_valid_token(app, account_id).await?;
            let url = format!(
                "https://graph.microsoft.com/v1.0/me/messages/{}/move",
                raw_id
            );
            let client = http_client();
            let resp = client
                .post(&url)
                .bearer_auth(&token)
                .json(&serde_json::json!({ "destinationId": destination_folder }))
                .send()
                .await
                .map_err(|e| e.to_string())?;
            if !resp.status().is_success() {
                return Err(format!("API error {}", resp.status().as_u16()));
            }
        }
    }
    Ok(())
}

/// IMAP helper: set or remove a flag on a message by UID. Connects + logs in,
/// then delegates the select/guard/STORE to the stream-generic `_in_session`
/// body so the same logic is driven by tests over a plaintext mock.
// IMAP connection + target parameters; inherent to the protocol call.
#[allow(clippy::too_many_arguments)]
fn imap_set_flag_sync(
    host: String,
    port: u16,
    username: String,
    password: String,
    folder: &str,
    uid: u32,
    flag: &str,
    add: bool,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    // Re-validate the stored host before reconnecting (DNS-rebinding defense;
    // honors the global bypass). Sync helper, already off the async runtime.
    widget_netguard::check_host_port(&host, port)?;
    let client = imap_connect(&host, port)?;
    let mut session = client
        .login(&username, &password)
        .map_err(|(e, _)| format!("IMAP login failed: {}", e))?;
    let result =
        imap_set_flag_in_session(&mut session, folder, uid, flag, add, expected_uid_validity);
    session.logout().ok();
    result
}

#[allow(clippy::too_many_arguments)]
fn imap_set_flag_in_session<S: std::io::Read + std::io::Write>(
    session: &mut imap::Session<S>,
    folder: &str,
    uid: u32,
    flag: &str,
    add: bool,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    let mailbox_name = resolve_imap_mailbox(session, folder);
    let selected = session
        .select(&mailbox_name)
        .map_err(|e| format!("Cannot select folder '{}': {}", mailbox_name, e))?;
    ensure_uid_validity(&selected, expected_uid_validity, "mark")?;

    let uid_str = uid.to_string();
    let op = if add {
        format!("+FLAGS ({})", flag)
    } else {
        format!("-FLAGS ({})", flag)
    };
    session
        .uid_store(&uid_str, op)
        .map_err(|e| format!("IMAP STORE failed: {}", e))?;
    Ok(())
}

/// IMAP helper: delete a message by UID (set \Deleted flag + UID EXPUNGE).
fn imap_delete_message_sync(
    host: String,
    port: u16,
    username: String,
    password: String,
    folder: &str,
    uid: u32,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    // Re-validate the stored host before reconnecting (DNS-rebinding defense;
    // honors the global bypass). Sync helper, already off the async runtime.
    widget_netguard::check_host_port(&host, port)?;
    let client = imap_connect(&host, port)?;
    let mut session = client
        .login(&username, &password)
        .map_err(|(e, _)| format!("IMAP login failed: {}", e))?;
    let result = imap_delete_message_in_session(&mut session, folder, uid, expected_uid_validity);
    session.logout().ok();
    result
}

fn imap_delete_message_in_session<S: std::io::Read + std::io::Write>(
    session: &mut imap::Session<S>,
    folder: &str,
    uid: u32,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    let mailbox_name = resolve_imap_mailbox(session, folder);
    let selected = session
        .select(&mailbox_name)
        .map_err(|e| format!("Cannot select folder '{}': {}", mailbox_name, e))?;
    ensure_uid_validity(&selected, expected_uid_validity, "delete")?;

    let uid_str = uid.to_string();
    session
        .uid_store(&uid_str, "+FLAGS (\\Deleted)")
        .map_err(|e| format!("IMAP STORE failed: {}", e))?;
    // UID-scoped expunge (UIDPLUS, RFC 4315): removes ONLY this UID. A bare
    // EXPUNGE permanently removes EVERY \Deleted message in the mailbox, so a
    // single delete could wipe other messages flagged \Deleted by another client
    // (data loss). On a server without UIDPLUS this errors and the delete fails,
    // which is strictly safer than the collateral purge.
    session
        .uid_expunge(&uid_str)
        .map_err(|e| format!("IMAP EXPUNGE failed: {}", e))?;
    Ok(())
}

/// IMAP helper: move a message by UID (server-side MOVE, atomic).
#[allow(clippy::too_many_arguments)]
fn imap_move_message_sync(
    host: String,
    port: u16,
    username: String,
    password: String,
    folder: &str,
    uid: u32,
    destination: &str,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    // Re-validate the stored host before reconnecting (DNS-rebinding defense;
    // honors the global bypass). Sync helper, already off the async runtime.
    widget_netguard::check_host_port(&host, port)?;
    let client = imap_connect(&host, port)?;
    let mut session = client
        .login(&username, &password)
        .map_err(|(e, _)| format!("IMAP login failed: {}", e))?;
    let result = imap_move_message_in_session(
        &mut session,
        folder,
        uid,
        destination,
        expected_uid_validity,
    );
    session.logout().ok();
    result
}

fn imap_move_message_in_session<S: std::io::Read + std::io::Write>(
    session: &mut imap::Session<S>,
    folder: &str,
    uid: u32,
    destination: &str,
    expected_uid_validity: Option<u32>,
) -> Result<(), String> {
    let destination = resolve_imap_mailbox(session, destination);
    let mailbox_name = resolve_imap_mailbox(session, folder);
    let selected = session
        .select(&mailbox_name)
        .map_err(|e| format!("Cannot select folder '{}': {}", mailbox_name, e))?;
    ensure_uid_validity(&selected, expected_uid_validity, "move")?;

    let uid_str = uid.to_string();
    // Atomic server-side MOVE (RFC 6851): copy + delete + expunge in one step,
    // scoped to this UID. The old COPY + STORE(\Deleted) + bare EXPUNGE both
    // purged every other \Deleted message in the source (data loss) AND, on a
    // partial failure between COPY and EXPUNGE, left a duplicate. uid_mv avoids
    // both; on a server without MOVE it errors and the move fails (safe).
    session
        .uid_mv(&uid_str, &destination)
        .map_err(|e| format!("IMAP MOVE failed: {}", e))?;
    Ok(())
}

// ---------------------------------------------------------------------------
// Open URL / message
// ---------------------------------------------------------------------------

pub fn open_url(url: &str) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    shell_open(url);
    Ok(())
}

/// The `ms-outlook://` protocol only understands Outlook/Graph IDs. Skip it
/// for IDs from other providers (e.g. Gmail): their IDs are not recognised
/// by Outlook and the deep link would fail silently.
fn is_outlook_id(composite_id: &str) -> bool {
    !composite_id.starts_with("google:")
        && !composite_id.starts_with("imap:")
        && !composite_id.starts_with("caldav:")
        && !composite_id.starts_with("carddav:")
}

/// Whether `web_link` is a real shell-openable target. IMAP messages are built
/// with `web_link: ""` (no web permalink exists); shell-opening an empty string
/// launches Explorer at the process's working dir (issue #45a). Callers must
/// treat a `false` result as "no surface to open", never fall through to a
/// shell-open. `trim` so a whitespace-only link is also rejected.
fn is_openable_web_link(web_link: &str) -> bool {
    !web_link.trim().is_empty()
}

pub fn open_message(web_link: &str, message_id: &str) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    if is_outlook_id(message_id) && ms_outlook_registered() {
        let encoded = percent_encode(message_id);
        let url = format!("ms-outlook://emails/{}", encoded);
        shell_open(&url);
        return Ok(());
    }
    // issue #45a: an IMAP message has no web view (web_link is empty). Opening
    // "" would ShellExecute an empty verb → Explorer at the install dir. Treat
    // "no link" as an explicit, logged no-op instead of a misleading window.
    if !is_openable_web_link(web_link) {
        log::info!(
            "email:open_message no web link for id '{}' (IMAP has no web view); nothing to open",
            message_id
        );
        return Ok(());
    }
    open_url(web_link)
}

pub fn open_calendar_event(web_link: &str, event_id: &str) -> Result<(), String> {
    // Calendar event_id is NOT composite (just the raw provider-specific id),
    // so disambiguate via web_link domain instead: only hand off to Outlook
    // when the link points at a Microsoft calendar surface.
    #[cfg(target_os = "windows")]
    if is_microsoft_web_link(web_link) && ms_outlook_registered() {
        let encoded = percent_encode(event_id);
        let url = format!("ms-outlook://events/{}", encoded);
        shell_open(&url);
        return Ok(());
    }
    if !web_link.is_empty() {
        return open_url(web_link);
    }
    Ok(())
}

#[cfg(target_os = "windows")]
fn is_microsoft_web_link(url: &str) -> bool {
    url.contains("outlook.office.com")
        || url.contains("outlook.live.com")
        || url.contains("outlook.office365.com")
        || url.contains("office.com")
}

pub fn reply_to_message(message_id: &str, from_email: &str, subject: &str) -> Result<(), String> {
    #[cfg(target_os = "windows")]
    if is_outlook_id(message_id) && ms_outlook_registered() {
        let encoded = percent_encode(message_id);
        let url = format!("ms-outlook://emails/{}/reply", encoded);
        shell_open(&url);
        return Ok(());
    }
    let re_subject = if subject.starts_with("Re:") || subject.starts_with("RE:") {
        subject.to_string()
    } else {
        format!("Re: {}", subject)
    };
    let mailto = format!(
        "mailto:{}?subject={}",
        from_email,
        percent_encode(&re_subject)
    );
    open_url(&mailto)
}

#[cfg(target_os = "windows")]
fn ms_outlook_registered() -> bool {
    use std::ptr;
    use winapi::um::winnt::KEY_READ;
    use winapi::um::winreg::{RegCloseKey, RegOpenKeyExW, HKEY_CLASSES_ROOT};

    let key: Vec<u16> = "ms-outlook\0".encode_utf16().collect();
    let mut hkey = ptr::null_mut();
    let ret = unsafe { RegOpenKeyExW(HKEY_CLASSES_ROOT, key.as_ptr(), 0, KEY_READ, &mut hkey) };
    if ret == 0 {
        unsafe { RegCloseKey(hkey) };
        true
    } else {
        false
    }
}

#[cfg(target_os = "windows")]
fn shell_open(url: &str) {
    use std::ptr;
    use winapi::um::shellapi::ShellExecuteW;
    let op: Vec<u16> = "open\0".encode_utf16().collect();
    let url_w: Vec<u16> = url.encode_utf16().chain(std::iter::once(0)).collect();
    unsafe {
        ShellExecuteW(
            ptr::null_mut(),
            op.as_ptr(),
            url_w.as_ptr(),
            ptr::null(),
            ptr::null(),
            1,
        );
    }
}

fn percent_encode(s: &str) -> String {
    s.bytes()
        .map(|b| match b {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                (b as char).to_string()
            }
            b => format!("%{:02X}", b),
        })
        .collect()
}

// ---------------------------------------------------------------------------
// Date formatting helpers
// ---------------------------------------------------------------------------

#[allow(dead_code)]
fn format_iso8601(secs: u64) -> String {
    let tod = secs % 86400;
    let sec = (tod % 60) as u32;
    let min = ((tod / 60) % 60) as u32;
    let hour = (tod / 3600) as u32;

    let d = secs / 86400;
    let z = d + 719468;
    let era = z / 146097;
    let doe = z % 146097;
    let yoe = (doe - doe / 1460 + doe / 36524 - doe / 146096) / 365;
    let y = yoe + era * 400;
    let doy = doe - (365 * yoe + yoe / 4 - yoe / 100);
    let mp = (5 * doy + 2) / 153;
    let day = doy - (153 * mp + 2) / 5 + 1;
    let mo = if mp < 10 { mp + 3 } else { mp - 9 };
    let yr = if mo <= 2 { y + 1 } else { y };

    format!(
        "{:04}-{:02}-{:02}T{:02}:{:02}:{:02}Z",
        yr, mo, day, hour, min, sec
    )
}

// ---------------------------------------------------------------------------
// iCal / vCard helpers (shared for CalDAV + CardDAV)
// ---------------------------------------------------------------------------

/// Unfold RFC 5545/6350 continuation lines (lines starting with space or tab).
fn unfold_ical(s: &str) -> String {
    let mut result = String::with_capacity(s.len());
    for line in s.lines() {
        if line.starts_with(' ') || line.starts_with('\t') {
            result.push_str(&line[1..]);
        } else {
            if !result.is_empty() {
                result.push('\n');
            }
            result.push_str(line);
        }
    }
    result
}

/// Extract the value of a named iCal/vCard property (ignores parameters).
fn get_ical_prop(ical: &str, name: &str) -> String {
    let name_lower = name.to_lowercase();
    for line in ical.lines() {
        let Some(colon) = line.find(':') else {
            continue;
        };
        let prop_name = line[..colon].split(';').next().unwrap_or("").to_lowercase();
        if prop_name == name_lower {
            return line[colon + 1..].trim().to_string();
        }
    }
    String::new()
}

/// Return the whole iCal/vCard line for a property, parameters included
/// (e.g. `ORGANIZER;CN="Alice":mailto:a@x`). Use where parameters carry data;
/// `get_ical_prop` would discard them.
fn get_ical_line(ical: &str, name: &str) -> String {
    let name_lower = name.to_lowercase();
    for line in ical.lines() {
        let end = line.find([';', ':']).unwrap_or(line.len());
        if line[..end].to_lowercase() == name_lower {
            return line.to_string();
        }
    }
    String::new()
}

/// "20260401T120000Z" → "2026-04-01T12:00:00Z"
fn caldav_dt_to_iso(dt: &str) -> String {
    let dt = dt.trim();
    // `.get()` returns None when the index is out of range OR not a UTF-8 char
    // boundary, so a malformed feed with a multi-byte char in the date prefix
    // can't cause a mid-character slice panic (was `&dt[0..4]` etc.).
    let (Some(year), Some(month), Some(day)) = (dt.get(0..4), dt.get(4..6), dt.get(6..8)) else {
        return dt.to_string();
    };

    if dt.len() >= 15 {
        if let Some(t) = dt.find('T') {
            let tp = &dt[t + 1..];
            let hour = tp.get(0..2).unwrap_or("00");
            let min = tp.get(2..4).unwrap_or("00");
            let sec = tp.get(4..6).unwrap_or("00");
            let z = if tp.ends_with('Z') { "Z" } else { "" };
            return format!("{}-{}-{}T{}:{}:{}{}", year, month, day, hour, min, sec, z);
        }
    }
    format!("{}-{}-{}", year, month, day)
}

/// Extract the `TZID` parameter (an IANA zone name) from a full iCal property
/// line, e.g. `DTSTART;TZID=America/New_York:20260401T120000` →
/// `Some("America/New_York")`. Parameter names are case-insensitive (RFC 5545)
/// and the value may be DQUOTE-wrapped. Returns `None` when no `TZID` is present.
fn ical_tzid(line: &str) -> Option<String> {
    let params_end = line.find(':').unwrap_or(line.len());
    // Skip the property name (first ';'-separated token); scan the parameters.
    for param in line[..params_end].split(';').skip(1) {
        let Some(eq) = param.find('=') else { continue };
        if param[..eq].trim().eq_ignore_ascii_case("TZID") {
            let val = param[eq + 1..].trim().trim_matches('"');
            if !val.is_empty() {
                return Some(val.to_string());
            }
        }
    }
    None
}

/// Convert an iCal DATE-TIME value plus its optional `TZID` parameter to an
/// ISO 8601 string, resolving a TZID-local wall-clock time to a true UTC instant.
///
/// issue #66: `get_ical_prop` strips the `TZID=` parameter, so a zoned
/// `DTSTART;TZID=America/New_York:20260401T120000` used to reach the frontend as
/// the naive `2026-04-01T12:00:00`, which it then read as UTC — rendering the
/// event 4h early on a GMT-4 account. Cases handled:
/// - trailing `Z` → already UTC (delegated to `caldav_dt_to_iso`);
/// - DATE only (no `T`, e.g. `VALUE=DATE`) → all-day, returned verbatim;
/// - `TZID` present → parse the local wall-clock time and convert the IANA zone
///   to UTC, emitting `...Z`. An unknown zone, an unparseable time, or a
///   nonexistent local time (spring-forward gap) is logged loudly and falls back
///   to the prior floating behavior (never panics);
/// - no zone at all → floating; the prior best-effort (naive, treated as UTC
///   downstream) is preserved.
fn caldav_dt_to_iso_tz(value: &str, tzid: Option<&str>) -> String {
    let v = value.trim();
    // Already UTC, a bare DATE, or too short to carry a time: the zone-free path
    // is already correct.
    if v.ends_with('Z') || !v.contains('T') {
        return caldav_dt_to_iso(v);
    }
    let Some(tzid) = tzid else {
        // Floating time (no TZID, no Z): unchanged best-effort.
        return caldav_dt_to_iso(v);
    };
    let tz: Tz = match tzid.parse() {
        Ok(tz) => tz,
        Err(_) => {
            log::warn!(
                "email:caldav unknown TZID '{}' on '{}'; treating as floating time",
                tzid,
                v
            );
            return caldav_dt_to_iso(v);
        }
    };
    let Ok(naive) = NaiveDateTime::parse_from_str(v, "%Y%m%dT%H%M%S") else {
        log::warn!(
            "email:caldav unparseable local datetime '{}' (TZID {}); treating as floating",
            v,
            tzid
        );
        return caldav_dt_to_iso(v);
    };
    // `.single()` covers the normal case; `.earliest()` resolves a fall-back DST
    // ambiguity deterministically. Both `None` → the local time does not exist
    // (spring-forward gap): log + fall back rather than fabricate an instant.
    let mapped = tz.from_local_datetime(&naive);
    match mapped.single().or_else(|| mapped.earliest()) {
        Some(dt) => dt
            .with_timezone(&Utc)
            .format("%Y-%m-%dT%H:%M:%SZ")
            .to_string(),
        None => {
            log::warn!(
                "email:caldav local time '{}' does not exist in zone '{}'; treating as floating",
                v,
                tzid
            );
            caldav_dt_to_iso(v)
        }
    }
}

/// "2026-04-01T00:00:00Z" → "20260401T000000Z"
fn iso_to_caldav_dt(iso: &str) -> String {
    iso.replace(['-', ':'], "")
}

/// Case-insensitive ASCII substring search returning the byte index of the
/// first match **in `haystack`**. Unlike `haystack.to_uppercase().find(needle)`,
/// the returned index is always valid for slicing `haystack`: case-folding can
/// change a string's byte length (e.g. `ß`→`SS`), so an offset taken from a
/// converted copy can land mid-character in the original and panic. `needle`
/// must be ASCII (the markers we search for, `CN=` and `mailto:`, are).
fn find_ci_ascii(haystack: &str, needle: &str) -> Option<usize> {
    let h = haystack.as_bytes();
    let n = needle.as_bytes();
    if n.is_empty() || h.len() < n.len() {
        return None;
    }
    (0..=h.len() - n.len()).find(|&i| h[i..i + n.len()].eq_ignore_ascii_case(n))
}

/// Decode common XML entities.
fn xml_decode(s: &str) -> String {
    s.replace("&amp;", "&")
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .replace("&quot;", "\"")
        .replace("&apos;", "'")
}

/// Parse all VEVENT blocks from a CalDAV REPORT response (may contain multiple ICS blobs).
fn parse_ics_events(text: &str) -> Vec<CalendarEvent> {
    let mut events = Vec::new();
    let mut search = text;

    while let Some(start) = search.find("BEGIN:VEVENT") {
        let rest = &search[start..];
        let Some(end_off) = rest.find("END:VEVENT") else {
            break;
        };
        let block = &rest[..end_off + "END:VEVENT".len()];
        let unfolded = unfold_ical(block);

        let uid = get_ical_prop(&unfolded, "UID");
        let summary = xml_decode(&get_ical_prop(&unfolded, "SUMMARY"));
        let dtstart_raw = get_ical_prop(&unfolded, "DTSTART");
        let dtend_raw = get_ical_prop(&unfolded, "DTEND");
        // Full lines so the TZID= parameter survives (get_ical_prop strips params);
        // needed to resolve a zoned wall-clock time to UTC (issue #66).
        let dtstart_line = get_ical_line(&unfolded, "DTSTART");
        let dtend_line = get_ical_line(&unfolded, "DTEND");
        let location_raw = get_ical_prop(&unfolded, "LOCATION");
        // Full line so the CN= parameter survives (get_ical_prop strips params).
        let organizer_line = get_ical_line(&unfolded, "ORGANIZER");

        let is_all_day = !dtstart_raw.contains('T');
        let start_dt = caldav_dt_to_iso_tz(&dtstart_raw, ical_tzid(&dtstart_line).as_deref());
        let end_dt = caldav_dt_to_iso_tz(&dtend_raw, ical_tzid(&dtend_line).as_deref());

        // Prefer the CN= display name; otherwise fall back to the mailto address.
        let organizer = if organizer_line.is_empty() {
            None
        } else {
            let name = if let Some(cn) = find_ci_ascii(&organizer_line, "CN=") {
                organizer_line[cn + 3..]
                    .split([',', ';', ':'])
                    .next()
                    .unwrap_or("")
                    .trim_matches('"')
                    .to_string()
            } else if let Some(m) = find_ci_ascii(&organizer_line, "mailto:") {
                organizer_line[m + "mailto:".len()..].trim().to_string()
            } else {
                get_ical_prop(&unfolded, "ORGANIZER")
            };
            if name.is_empty() {
                None
            } else {
                Some(name)
            }
        };

        events.push(CalendarEvent {
            id: if uid.is_empty() {
                format!("caldav:{}", events.len())
            } else {
                uid
            },
            subject: if summary.is_empty() {
                "(no title)".into()
            } else {
                summary
            },
            start_dt,
            end_dt,
            is_all_day,
            location: if location_raw.is_empty() {
                None
            } else {
                Some(xml_decode(&location_raw))
            },
            organizer,
            web_link: String::new(),
        });

        search = &rest[end_off + "END:VEVENT".len()..];
    }
    events
}

/// Parse all VCARD blocks from a CardDAV REPORT response.
fn parse_vcards(text: &str) -> Vec<Contact> {
    let mut contacts = Vec::new();
    let mut search = text;

    while let Some(start) = search.find("BEGIN:VCARD") {
        let rest = &search[start..];
        let Some(end_off) = rest.find("END:VCARD") else {
            break;
        };
        let block = &rest[..end_off + "END:VCARD".len()];
        let unfolded = unfold_ical(block);

        let fn_ = xml_decode(&get_ical_prop(&unfolded, "FN"));
        if fn_.is_empty() {
            search = &rest[end_off + "END:VCARD".len()..];
            continue;
        }

        let email_raw = get_ical_prop(&unfolded, "EMAIL");
        let tel_raw = get_ical_prop(&unfolded, "TEL");
        let org_raw = get_ical_prop(&unfolded, "ORG");
        let title = get_ical_prop(&unfolded, "TITLE");
        let uid = get_ical_prop(&unfolded, "UID");

        // EMAIL may have "TYPE=INTERNET,HOME:" prefix, take last segment after ':'
        let email_clean = if email_raw.contains(':') {
            email_raw
                .split(':')
                .next_back()
                .unwrap_or(&email_raw)
                .to_string()
        } else {
            email_raw
        };

        // ORG may be "Company;Department", only take first part
        let company = org_raw.split(';').next().unwrap_or("").trim().to_string();

        contacts.push(Contact {
            id: if uid.is_empty() {
                format!("carddav:{}", contacts.len())
            } else {
                uid
            },
            display_name: fn_,
            email: if email_clean.is_empty() {
                None
            } else {
                Some(email_clean)
            },
            phone: if tel_raw.is_empty() {
                None
            } else {
                Some(tel_raw)
            },
            job_title: if title.is_empty() { None } else { Some(title) },
            company: if company.is_empty() {
                None
            } else {
                Some(company)
            },
        });

        search = &rest[end_off + "END:VCARD".len()..];
    }
    contacts
}

// ---------------------------------------------------------------------------
// CalDAV / CardDAV service discovery (RFC 6764 bootstrapping)  [S-45]
//
// Real DAV clients don't demand the exact collection URL: given a base server
// address — or nothing but the domain, via `.well-known` — they walk
// current-user-principal -> {calendar,addressbook}-home-set -> the collections
// underneath. Themia used to PROPFIND *exactly* the URL typed, so a Nextcloud
// user who pasted the server root (the address their browser shows) got a bare
// HTTP 401 — the web root rejects a basic-auth PROPFIND — even though CardDAV,
// where they'd pasted a real collection path, connected fine (themia-releases
// #71: "CardDAV works but not CalDAV on Nextcloud, ERROR 401").
//
// Discovery is an ADDITIVE fallback: a URL that already resolves to a calendar
// / addressbook collection is still used verbatim (one round-trip, unchanged),
// so existing accounts and exact-URL entries never change behavior. Only when
// the typed URL isn't itself a usable collection do we chase the chain, and
// only against the same host the probe already reached.
//
// The XML is parsed with small prefix-insensitive scanners (servers vary the
// namespace prefix freely: `D:`, `d:`, `cal:`, default-ns), matching the
// hand-rolled style of the iCal/vCard parsers above rather than pulling in an
// XML crate for a handful of well-known element names.
// ---------------------------------------------------------------------------

#[derive(Clone, Copy, PartialEq, Eq)]
enum DavKind {
    Cal,
    Card,
}

impl DavKind {
    /// RFC 6764 `.well-known` path segment.
    fn well_known(self) -> &'static str {
        match self {
            DavKind::Cal => "caldav",
            DavKind::Card => "carddav",
        }
    }
    /// Local-name of the resourcetype that marks a usable collection.
    fn collection_type(self) -> &'static str {
        match self {
            DavKind::Cal => "calendar",
            DavKind::Card => "addressbook",
        }
    }
    /// Local-name of the home-set property.
    fn home_set(self) -> &'static str {
        match self {
            DavKind::Cal => "calendar-home-set",
            DavKind::Card => "addressbook-home-set",
        }
    }
    /// Namespace URI for the kind-specific properties.
    fn ns(self) -> &'static str {
        match self {
            DavKind::Cal => "urn:ietf:params:xml:ns:caldav",
            DavKind::Card => "urn:ietf:params:xml:ns:carddav",
        }
    }
}

/// Locate the first XML element whose *local* name (prefix stripped,
/// ASCII-case-insensitive) equals `local`, at or after `from`. Returns
/// `(open_tag_start, after_open_tag, self_closing)` byte offsets. Skips close
/// tags, comments, PIs and declarations (`</`, `<!`, `<?`).
fn dav_open_tag(xml: &str, local: &str, from: usize) -> Option<(usize, usize, bool)> {
    let bytes = xml.as_bytes();
    let mut i = from.min(xml.len());
    while let Some(rel) = xml[i..].find('<') {
        let lt = i + rel;
        let j0 = lt + 1;
        match bytes.get(j0) {
            Some(b'/') | Some(b'!') | Some(b'?') => {
                i = j0;
                continue;
            }
            None => return None,
            _ => {}
        }
        let mut j = j0;
        while j < bytes.len() && !matches!(bytes[j], b' ' | b'\t' | b'\r' | b'\n' | b'/' | b'>') {
            j += 1;
        }
        let name = &xml[j0..j];
        let local_name = name.rsplit(':').next().unwrap_or(name);
        if local_name.eq_ignore_ascii_case(local) {
            let gt = j + xml[j..].find('>')?;
            let self_closing = gt > 0 && bytes[gt - 1] == b'/';
            return Some((lt, gt + 1, self_closing));
        }
        i = j;
    }
    None
}

/// Start offset of the matching close tag `</[pfx:]local>` at or after `from`.
fn dav_close_tag(xml: &str, local: &str, from: usize) -> Option<usize> {
    let bytes = xml.as_bytes();
    let mut i = from.min(xml.len());
    while let Some(rel) = xml[i..].find("</") {
        let lt = i + rel;
        let j0 = lt + 2;
        let mut j = j0;
        while j < bytes.len() && !matches!(bytes[j], b' ' | b'\t' | b'\r' | b'\n' | b'>') {
            j += 1;
        }
        let name = &xml[j0..j];
        let local_name = name.rsplit(':').next().unwrap_or(name);
        if local_name.eq_ignore_ascii_case(local) {
            return Some(lt);
        }
        i = j.max(lt + 2);
    }
    None
}

/// Inner markup/text of the first `local` element. `Some("")` for a
/// self-closing or empty element; `None` when the element is absent. Does not
/// track nesting — sufficient for the flat, non-recursive DAV elements used
/// here (href, current-user-principal, *-home-set, resourcetype, response).
fn dav_inner<'a>(xml: &'a str, local: &str) -> Option<&'a str> {
    let (_, after_open, self_closing) = dav_open_tag(xml, local, 0)?;
    if self_closing {
        return Some("");
    }
    let close = dav_close_tag(xml, local, after_open)?;
    Some(&xml[after_open..close])
}

/// True when `xml` contains a start/empty tag with local-name `local`.
fn dav_has(xml: &str, local: &str) -> bool {
    dav_open_tag(xml, local, 0).is_some()
}

/// First `<href>` inside a fragment, entity-decoded and trimmed.
fn dav_href(fragment: &str) -> Option<String> {
    let raw = dav_inner(fragment, "href")?;
    let v = xml_decode(raw.trim());
    if v.is_empty() {
        None
    } else {
        Some(v)
    }
}

/// Each `<response>…</response>` block's inner markup, in document order.
fn dav_responses(xml: &str) -> Vec<&str> {
    let mut out = Vec::new();
    let mut from = 0;
    while let Some((_, after_open, self_closing)) = dav_open_tag(xml, "response", from) {
        if self_closing {
            from = after_open;
            continue;
        }
        let Some(close) = dav_close_tag(xml, "response", after_open) else {
            break;
        };
        out.push(&xml[after_open..close]);
        from = close + 1;
    }
    out
}

/// `current-user-principal` href from a PROPFIND response, if present.
fn dav_principal(xml: &str) -> Option<String> {
    dav_href(dav_inner(xml, "current-user-principal")?)
}

/// Kind-specific home-set href from a PROPFIND response, if present.
fn dav_home_set(xml: &str, kind: DavKind) -> Option<String> {
    dav_href(dav_inner(xml, kind.home_set())?)
}

/// True when a Depth-0 probe says the requested resource is *itself* a
/// collection of the wanted kind (so it can be used verbatim).
fn dav_probe_is_collection(xml: &str, kind: DavKind) -> bool {
    dav_responses(xml).iter().any(|resp| {
        dav_has(
            dav_inner(resp, "resourcetype").unwrap_or(""),
            kind.collection_type(),
        )
    })
}

/// Collection hrefs from a home-set enumeration (Depth 1) whose resourcetype
/// marks them as the wanted kind. For calendars, a present
/// `supported-calendar-component-set` must include VEVENT (skips a tasks-only
/// or subscription/birthday-only collection); an absent component set is
/// accepted leniently.
fn dav_collections(xml: &str, kind: DavKind) -> Vec<String> {
    let mut out = Vec::new();
    for resp in dav_responses(xml) {
        let rtype = dav_inner(resp, "resourcetype").unwrap_or("");
        if !dav_has(rtype, kind.collection_type()) {
            continue;
        }
        if kind == DavKind::Cal {
            if let Some(comps) = dav_inner(resp, "supported-calendar-component-set") {
                if find_ci_ascii(comps, "VEVENT").is_none() {
                    continue;
                }
            }
        }
        if let Some(href) = dav_href(resp) {
            out.push(href);
        }
    }
    out
}

/// Last non-empty path segment of an href, used as a calendar's fallback name
/// when the server omits a `displayname`.
fn dav_last_segment(href: &str) -> String {
    href.trim_end_matches('/')
        .rsplit('/')
        .find(|s| !s.is_empty())
        .unwrap_or(href)
        .to_string()
}

/// Parse a Depth-1 home-set enumeration into calendar entries [S-48]: each
/// VEVENT-capable collection's href (id), `displayname` (name; falls back to the
/// last path segment) and `calendar-color` (normalized). Mirrors
/// `dav_collections`' VEVENT filter so a tasks-only / subscription collection is
/// skipped. Hrefs stay raw — the async caller resolves them against the
/// enumeration URL and flags the default.
fn dav_calendars(xml: &str) -> Vec<CalendarInfo> {
    let mut out = Vec::new();
    for resp in dav_responses(xml) {
        let rtype = dav_inner(resp, "resourcetype").unwrap_or("");
        if !dav_has(rtype, DavKind::Cal.collection_type()) {
            continue;
        }
        if let Some(comps) = dav_inner(resp, "supported-calendar-component-set") {
            if find_ci_ascii(comps, "VEVENT").is_none() {
                continue;
            }
        }
        let Some(href) = dav_href(resp) else { continue };
        let name = dav_inner(resp, "displayname")
            .map(|s| xml_decode(s.trim()))
            .filter(|s| !s.is_empty())
            .unwrap_or_else(|| dav_last_segment(&href));
        let color =
            normalize_cal_color(dav_inner(resp, "calendar-color").map(|s| s.trim().to_string()));
        out.push(CalendarInfo {
            id: href,
            name,
            is_default: false,
            color,
        });
    }
    out
}

/// `scheme://authority` for `url`, or `None` when it lacks a scheme+authority.
fn dav_origin(url: &str) -> Option<String> {
    let scheme_end = url.find("://")?;
    let scheme = &url[..scheme_end];
    if scheme.is_empty() {
        return None;
    }
    let authority = url[scheme_end + 3..].split('/').next().unwrap_or("");
    if authority.is_empty() {
        return None;
    }
    Some(format!("{scheme}://{authority}"))
}

/// Resolve a DAV `href` — usually an absolute path, sometimes a full URL, rarely
/// relative — against the URL it was fetched from.
fn dav_resolve(base: &str, href: &str) -> String {
    let href = href.trim();
    if href.starts_with("http://") || href.starts_with("https://") {
        return href.to_string();
    }
    let Some(origin) = dav_origin(base) else {
        return href.to_string();
    };
    if let Some(path) = href.strip_prefix('/') {
        return format!("{origin}/{path}");
    }
    // Relative: join to the base's directory.
    let after_origin = &base[origin.len()..];
    let dir_end = after_origin
        .rfind('/')
        .map(|i| origin.len() + i + 1)
        .unwrap_or(base.len());
    format!("{}{}", &base[..dir_end], href)
}

fn dav_discovery_body(kind: DavKind) -> String {
    format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:" xmlns:A="{ns}">
  <D:prop><D:resourcetype/><D:current-user-principal/><A:{home}/></D:prop>
</D:propfind>"#,
        ns = kind.ns(),
        home = kind.home_set()
    )
}

fn dav_home_body(kind: DavKind) -> String {
    format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:" xmlns:A="{ns}">
  <D:prop><A:{home}/></D:prop>
</D:propfind>"#,
        ns = kind.ns(),
        home = kind.home_set()
    )
}

fn dav_enum_body(kind: DavKind) -> String {
    match kind {
        DavKind::Cal => r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
  <D:prop><D:resourcetype/><D:displayname/><C:supported-calendar-component-set/></D:prop>
</D:propfind>"#
            .to_string(),
        DavKind::Card => r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:">
  <D:prop><D:resourcetype/><D:displayname/></D:prop>
</D:propfind>"#
            .to_string(),
    }
}

/// PROPFIND body for listing calendars [S-48]: like `dav_enum_body(Cal)` but
/// also asks for the Apple `calendar-color` so `dav_calendars` can surface a
/// per-calendar hue when the server provides one.
fn dav_calendar_list_body() -> String {
    r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:A="http://apple.com/ns/ical/">
  <D:prop><D:resourcetype/><D:displayname/><C:supported-calendar-component-set/><A:calendar-color/></D:prop>
</D:propfind>"#
        .to_string()
}

struct DavResp {
    status: u16,
    body: String,
    /// Final URL after any redirects — the base for resolving relative hrefs.
    url: String,
}

/// A PROPFIND that follows redirects *manually* so the method and body survive.
/// reqwest's automatic redirect would downgrade a `.well-known` 301 to GET (and
/// drop the body), breaking bootstrapping — so discovery uses a non-redirecting
/// client and re-issues the PROPFIND, re-attaching basic auth each hop (auth is
/// therefore never lost across a same-host or cross-host redirect).
async fn dav_propfind(
    client: &reqwest::Client,
    url: &str,
    username: &str,
    password: &str,
    body: String,
    depth: &str,
) -> Result<DavResp, String> {
    let method = reqwest::Method::from_bytes(b"PROPFIND").map_err(|e| e.to_string())?;
    let mut current = url.to_string();
    for _hop in 0..5 {
        let resp = client
            .request(method.clone(), &current)
            .basic_auth(username, Some(password))
            .header("Depth", depth)
            .header("Content-Type", "application/xml; charset=utf-8")
            .body(body.clone())
            .send()
            .await
            .map_err(|e| format!("Connection failed: {}", e))?;
        let status = resp.status().as_u16();
        if matches!(status, 301 | 302 | 307 | 308) {
            if let Some(loc) = resp
                .headers()
                .get(reqwest::header::LOCATION)
                .and_then(|v| v.to_str().ok())
            {
                let next = dav_resolve(&current, loc);
                if next != current {
                    current = next;
                    continue;
                }
            }
        }
        let url = resp.url().to_string();
        let body = resp.text().await.unwrap_or_default();
        return Ok(DavResp { status, body, url });
    }
    Err("Too many redirects".to_string())
}

fn dav_status_ok(status: u16) -> bool {
    status == 207 || (200..300).contains(&status)
}

/// Resolve the URL the user typed to a concrete calendar/address-book
/// collection, verifying credentials along the way. [S-45]
async fn dav_discover_collection(
    client: &reqwest::Client,
    entered: &str,
    username: &str,
    password: &str,
    kind: DavKind,
) -> Result<String, String> {
    // Candidate roots: what was typed, then the RFC 6764 `.well-known` URL — so
    // a bare domain / server root resolves even though its web root 401s a
    // PROPFIND.
    let mut candidates = vec![entered.to_string()];
    if let Some(origin) = dav_origin(entered) {
        let wk = format!("{}/.well-known/{}", origin, kind.well_known());
        if !candidates.iter().any(|c| c == &wk) {
            candidates.push(wk);
        }
    }

    let mut auth_failed = false;
    let mut last_status = 0u16;

    for cand in candidates {
        let probe = dav_propfind(
            client,
            &cand,
            username,
            password,
            dav_discovery_body(kind),
            "0",
        )
        .await?;
        if probe.status == 401 || probe.status == 403 {
            auth_failed = true;
            last_status = probe.status;
            continue;
        }
        if !dav_status_ok(probe.status) {
            last_status = probe.status;
            continue;
        }
        // Already a usable collection? Use it verbatim (redirect-normalized).
        if dav_probe_is_collection(&probe.body, kind) {
            return Ok(probe.url);
        }
        if let Some(found) = dav_walk_to_collection(client, &probe, username, password, kind).await?
        {
            return Ok(found);
        }
    }

    if auth_failed {
        return Err(format!(
            "Server returned HTTP {}, check URL and credentials",
            if last_status != 0 { last_status } else { 401 }
        ));
    }
    if last_status != 0 {
        return Err(format!(
            "Server returned HTTP {}, check URL and credentials",
            last_status
        ));
    }
    Err(match kind {
        DavKind::Cal => {
            "Connected, but no calendar was found there. Enter your calendar's full CalDAV URL."
                .to_string()
        }
        DavKind::Card => {
            "Connected, but no address book was found there. Enter your address book's full \
             CardDAV URL."
                .to_string()
        }
    })
}

/// From a connected probe response, walk current-user-principal -> home-set ->
/// collections. Returns the first usable collection URL, or `None` when the
/// chain dead-ends (the caller then reports a not-found error).
async fn dav_walk_to_collection(
    client: &reqwest::Client,
    probe: &DavResp,
    username: &str,
    password: &str,
    kind: DavKind,
) -> Result<Option<String>, String> {
    // The probe body may already carry the home-set (it hit the DAV root or a
    // principal); otherwise chase the principal for it.
    let home = if let Some(h) = dav_home_set(&probe.body, kind) {
        dav_resolve(&probe.url, &h)
    } else if let Some(principal) = dav_principal(&probe.body) {
        let purl = dav_resolve(&probe.url, &principal);
        let presp = dav_propfind(client, &purl, username, password, dav_home_body(kind), "0").await?;
        if !dav_status_ok(presp.status) {
            return Ok(None);
        }
        match dav_home_set(&presp.body, kind) {
            Some(h) => dav_resolve(&presp.url, &h),
            None => return Ok(None),
        }
    } else {
        return Ok(None);
    };

    // Enumerate the home-set (Depth 1); pick the first usable collection.
    let enun = dav_propfind(client, &home, username, password, dav_enum_body(kind), "1").await?;
    if !dav_status_ok(enun.status) {
        return Ok(None);
    }
    Ok(dav_collections(&enun.body, kind)
        .into_iter()
        .next()
        .map(|href| dav_resolve(&enun.url, &href)))
}

/// Shared non-redirecting HTTP client for DAV discovery (see `dav_propfind`).
fn dav_client() -> reqwest::Client {
    use std::sync::OnceLock;
    static CLIENT: OnceLock<reqwest::Client> = OnceLock::new();
    CLIENT
        .get_or_init(|| {
            widget_netproxy::client_builder(HTTP_TIMEOUT)
                .redirect(reqwest::redirect::Policy::none())
                .build()
                .unwrap_or_else(|e| {
                    log::error!("email: DAV client build failed ({e}); no proxy support");
                    reqwest::Client::new()
                })
        })
        .clone()
}

// ---------------------------------------------------------------------------
// Endpoint probe (connect presets) — [S-46]
//
// The connect form ships presets with each provider's published IMAP/DAV
// settings. Those settings drift, so before the user types a password the app
// checks, from this machine, that the endpoint still answers, and badges the
// preset when it doesn't.
//
// Anonymous by design: an IMAP greeting and an unauthenticated PROPFIND are the
// handshakes every mail client performs before logging in. No credentials are
// sent and nothing is written, so a probe can never leak a secret or mutate a
// mailbox. Reachability only — a green probe is not a sign-in; `add_*_account`
// remains the thing that verifies credentials.
// ---------------------------------------------------------------------------

/// Probes are a pre-flight check, so they fail fast rather than hanging the UI
/// for the 30s a real sync is allowed.
const PROBE_TIMEOUT: std::time::Duration = std::time::Duration::from_secs(10);

/// What to contact. Tagged union rather than a loose `kind: String` +
/// `Option<..>` bag so an impossible combination can't be constructed; mirrors
/// `PresetProbe` in src/connect/presets.ts.
#[derive(Debug, Deserialize)]
#[serde(tag = "kind", rename_all = "lowercase")]
pub enum ProbeTarget {
    Imap { host: String, port: u16 },
    Dav { url: String },
}

/// Closed outcome set (CLAUDE.md: closed sets are enums, not strings). Serializes
/// to `"reachable" | "unreachable" | "not_dav" | "server_error"`.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum ProbeOutcome {
    /// The endpoint answered as its protocol expects. The preset is current.
    Reachable,
    /// No answer at all: DNS, TCP, TLS, or timeout.
    Unreachable,
    /// Something answered, but not a DAV collection (404/405/…): the path moved,
    /// which is precisely how a stale preset presents.
    NotDav,
    /// The endpoint is there but unwell (5xx). Usually transient, so this is
    /// reported apart from a genuinely wrong address.
    ServerError,
}

#[derive(Debug, Serialize)]
pub struct ProbeResult {
    pub outcome: ProbeOutcome,
    /// Short human-readable evidence (greeting line, HTTP status, error text).
    pub detail: String,
    pub elapsed_ms: u64,
}

/// Classify an unauthenticated PROPFIND. 401/403 is the *healthy* answer — the
/// collection exists and wants credentials, which is all a probe can learn
/// without them. A `DAV:` response header proves it outright, whatever the
/// status. Pure so it is unit-tested without a socket.
fn classify_dav_status(status: u16, has_dav_header: bool) -> ProbeOutcome {
    if has_dav_header {
        return ProbeOutcome::Reachable;
    }
    match status {
        200..=299 | 401 | 403 => ProbeOutcome::Reachable,
        // Discovery follows redirects itself, so a 3xx here still means "this
        // address is live and points somewhere" — good enough for reachability.
        300..=399 => ProbeOutcome::Reachable,
        500..=599 => ProbeOutcome::ServerError,
        _ => ProbeOutcome::NotDav,
    }
}

/// Open IMAPS and read the greeting, then drop the connection. A server that
/// speaks IMAP opens with `* OK` or `* PREAUTH`.
fn probe_imap_blocking(host: &str, port: u16) -> Result<String, String> {
    let tls = native_tls::TlsConnector::builder()
        .build()
        .map_err(|e| format!("TLS error: {}", e))?;
    let sock = widget_netproxy::connect_tcp(host, port, PROBE_TIMEOUT)?;
    let stream = tls
        .connect(host, sock)
        .map_err(|e| format!("TLS handshake failed: {}", e))?;
    let mut client = imap::Client::new(stream);
    let greeting = client
        .read_greeting()
        .map_err(|e| format!("No IMAP greeting: {}", e))?;
    // `read_greeting` yields the raw bytes; keep the first line as evidence.
    let text = String::from_utf8_lossy(&greeting);
    Ok(text.lines().next().unwrap_or("").trim().to_string())
}

/// Contact a preset's endpoint without credentials. Always resolves to a
/// `ProbeResult`: an unreachable server is an *answer* the UI renders as a
/// badge, not an error the caller has to interpret.
pub async fn probe_endpoint(target: ProbeTarget) -> ProbeResult {
    let started = std::time::Instant::now();
    let outcome_detail = match target {
        ProbeTarget::Imap { host, port } => {
            match tokio::task::spawn_blocking(move || probe_imap_blocking(&host, port)).await {
                Ok(Ok(greeting)) => {
                    if greeting.starts_with("* OK") || greeting.starts_with("* PREAUTH") {
                        (ProbeOutcome::Reachable, greeting)
                    } else {
                        // Answered, but not in IMAP: the host is no longer a
                        // mail endpoint (parked domain, redirector, …).
                        (ProbeOutcome::NotDav, format!("Unexpected greeting: {}", greeting))
                    }
                }
                Ok(Err(e)) => (ProbeOutcome::Unreachable, e),
                Err(e) => (ProbeOutcome::Unreachable, e.to_string()),
            }
        }
        ProbeTarget::Dav { url } => {
            let method = match reqwest::Method::from_bytes(b"PROPFIND") {
                Ok(m) => m,
                Err(e) => {
                    return ProbeResult {
                        outcome: ProbeOutcome::Unreachable,
                        detail: e.to_string(),
                        elapsed_ms: started.elapsed().as_millis() as u64,
                    }
                }
            };
            let body = r#"<?xml version="1.0" encoding="UTF-8"?>
<D:propfind xmlns:D="DAV:"><D:prop><D:current-user-principal/></D:prop></D:propfind>"#;
            let req = dav_client()
                .request(method, &url)
                .header("Depth", "0")
                .header("Content-Type", "application/xml; charset=utf-8")
                .timeout(PROBE_TIMEOUT)
                .body(body);
            match req.send().await {
                Ok(resp) => {
                    let status = resp.status().as_u16();
                    let has_dav = resp.headers().contains_key("dav");
                    (
                        classify_dav_status(status, has_dav),
                        format!("HTTP {}{}", status, if has_dav { " (DAV)" } else { "" }),
                    )
                }
                Err(e) => (ProbeOutcome::Unreachable, e.to_string()),
            }
        }
    };

    ProbeResult {
        outcome: outcome_detail.0,
        detail: outcome_detail.1,
        elapsed_ms: started.elapsed().as_millis() as u64,
    }
}

// ---------------------------------------------------------------------------
// CalDAV fetch
// ---------------------------------------------------------------------------

async fn fetch_caldav_events(
    url: &str,
    username: &str,
    password: &str,
    start_dt: &str,
    end_dt: &str,
) -> Result<Vec<CalendarEvent>, String> {
    // Re-validate the STORED url at fetch time, not just when the account was
    // added (DNS-rebinding defense: a host that resolved public at add-time
    // could now resolve to a private/loopback address). Honors the global
    // bypass. Blocking DNS → off the async runtime via spawn_blocking.
    {
        let u = url.to_string();
        tokio::task::spawn_blocking(move || widget_netguard::check_url(&u))
            .await
            .map_err(|e| e.to_string())??;
    }

    let start_caldav = iso_to_caldav_dt(start_dt);
    let end_caldav = iso_to_caldav_dt(end_dt);

    let body = format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<C:calendar-query xmlns:C="urn:ietf:params:xml:ns:caldav" xmlns:D="DAV:">
  <D:prop><D:getetag/><C:calendar-data/></D:prop>
  <C:filter>
    <C:comp-filter name="VCALENDAR">
      <C:comp-filter name="VEVENT">
        <C:time-range start="{}" end="{}"/>
      </C:comp-filter>
    </C:comp-filter>
  </C:filter>
</C:calendar-query>"#,
        start_caldav, end_caldav
    );

    let client = http_client();

    let resp = client
        .request(reqwest::Method::from_bytes(b"REPORT").unwrap(), url)
        .basic_auth(username, Some(password))
        .header("Depth", "1")
        .header("Content-Type", "application/xml; charset=utf-8")
        .body(body)
        .send()
        .await
        .map_err(|e| format!("CalDAV request failed: {}", e))?;

    let status = resp.status().as_u16();
    if status != 207 && !resp.status().is_success() {
        return Err(format!("CalDAV server error: HTTP {}", status));
    }

    let text = resp.text().await.map_err(|e| e.to_string())?;
    Ok(parse_ics_events(&text))
}

// ---------------------------------------------------------------------------
// CardDAV fetch
// ---------------------------------------------------------------------------

async fn fetch_carddav_contacts(
    url: &str,
    username: &str,
    password: &str,
    search: &str,
    count: u32,
) -> Result<Vec<Contact>, String> {
    // Re-validate the stored url at fetch time (DNS-rebinding defense; honors
    // the global bypass). Blocking DNS → off the async runtime.
    {
        let u = url.to_string();
        tokio::task::spawn_blocking(move || widget_netguard::check_url(&u))
            .await
            .map_err(|e| e.to_string())??;
    }

    let body = r#"<?xml version="1.0" encoding="UTF-8"?>
<C:addressbook-query xmlns:C="urn:ietf:params:xml:ns:carddav" xmlns:D="DAV:">
  <D:prop><D:getetag/><C:address-data/></D:prop>
  <C:filter/>
</C:addressbook-query>"#;

    let client = http_client();

    let resp = client
        .request(reqwest::Method::from_bytes(b"REPORT").unwrap(), url)
        .basic_auth(username, Some(password))
        .header("Depth", "1")
        .header("Content-Type", "application/xml; charset=utf-8")
        .body(body)
        .send()
        .await
        .map_err(|e| format!("CardDAV request failed: {}", e))?;

    let status = resp.status().as_u16();
    if status != 207 && !resp.status().is_success() {
        return Err(format!("CardDAV server error: HTTP {}", status));
    }

    let text = resp.text().await.map_err(|e| e.to_string())?;
    let mut contacts = parse_vcards(&text);

    if !search.is_empty() {
        let q = search.to_lowercase();
        contacts.retain(|c| {
            c.display_name.to_lowercase().contains(&q)
                || c.email.as_deref().unwrap_or("").to_lowercase().contains(&q)
                || c.company
                    .as_deref()
                    .unwrap_or("")
                    .to_lowercase()
                    .contains(&q)
        });
    }

    contacts.sort_by(|a, b| a.display_name.cmp(&b.display_name));
    contacts.truncate(count as usize);
    Ok(contacts)
}

// ---------------------------------------------------------------------------
// Calendar
// ---------------------------------------------------------------------------

#[derive(Serialize, Clone)]
pub struct CalendarEvent {
    pub id: String,
    pub subject: String,
    pub start_dt: String,
    pub end_dt: String,
    pub is_all_day: bool,
    pub location: Option<String>,
    pub organizer: Option<String>,
    pub web_link: String,
}

/// One calendar within an account, for the calendar-picker UI [S-48].
#[derive(Serialize, Clone, Debug, PartialEq)]
pub struct CalendarInfo {
    pub id: String,
    pub name: String,
    pub is_default: bool,
    pub color: Option<String>,
}

/// Keep only a well-formed hex color, normalized to `#rrggbb`. Accepts the
/// Apple/CalDAV `#rrggbbaa` form (alpha dropped) and rejects Graph's `""` /
/// `"auto"` and any garbage so the frontend cleanly falls back to its palette
/// [S-51]. Persisted/remote color strings are untrusted.
fn normalize_cal_color(c: Option<String>) -> Option<String> {
    let s = c?;
    let s = s.trim();
    let b = s.as_bytes();
    if (b.len() == 7 || b.len() == 9)
        && b[0] == b'#'
        && b[1..].iter().all(|x| x.is_ascii_hexdigit())
    {
        Some(s[..7].to_ascii_lowercase())
    } else {
        None
    }
}

#[derive(Deserialize)]
struct GraphCalendarResp {
    value: Vec<GraphEvent>,
    #[serde(rename = "@odata.nextLink", default)]
    next_link: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphEvent {
    id: String,
    subject: Option<String>,
    start: Option<GraphDt>,
    end: Option<GraphDt>,
    is_all_day: bool,
    location: Option<GraphLocation>,
    organizer: Option<GraphOrganizer2>,
    web_link: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphDt {
    date_time: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphLocation {
    display_name: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphOrganizer2 {
    email_address: Option<GraphEmailAddress>,
}

pub async fn get_calendar_events(
    app: &tauri::AppHandle,
    account_id: &str,
    start_dt: &str,
    end_dt: &str,
    calendar_id: Option<&str>,
) -> Result<Vec<CalendarEvent>, String> {
    let accounts = load_accounts(app);
    let account = accounts
        .get(account_id)
        .ok_or_else(|| "Account not found".to_string())?
        .clone();

    match account.provider.as_str() {
        "caldav" => {
            // A `calendar_id` is a fully-resolved collection URL from
            // `list_calendars` [S-50]; absent → the account's stored default
            // collection. Both are REPORT-ed by the same fetch path [S-36].
            let stored = account.caldav_url.ok_or("No CalDAV URL configured")?;
            let url = calendar_id.unwrap_or(&stored);
            let username = account.username.unwrap_or_default();
            let password = account.password.unwrap_or_default();
            fetch_caldav_events(url, &username, &password, start_dt, end_dt).await
        }
        "google" => {
            let token = get_valid_token(app, account_id).await?;
            let client = http_client();
            google_calendar_core(
                "https://www.googleapis.com",
                &token,
                &client,
                start_dt,
                end_dt,
                calendar_id,
            )
            .await
        }
        _ => {
            // Microsoft Graph
            let token = get_valid_token(app, account_id).await?;
            let client = http_client();
            graph_calendar_core(
                "https://graph.microsoft.com",
                &token,
                &client,
                start_dt,
                end_dt,
                calendar_id,
            )
            .await
        }
    }
}

/// List every VEVENT calendar collection reachable from a CalDAV account [S-48].
/// Walks the stored collection URL → calendar-home-set → a Depth-1 enumeration,
/// resolves each href, and flags the collection matching the stored URL as the
/// default (else the first). Degrades gracefully to the single stored collection
/// when the home-set can't be discovered, so listing never fails harder than the
/// existing single-calendar path. Re-validates the URL (DNS-rebinding defense).
async fn caldav_list_calendars(
    url: &str,
    username: &str,
    password: &str,
) -> Result<Vec<CalendarInfo>, String> {
    {
        let u = url.to_string();
        tokio::task::spawn_blocking(move || widget_netguard::check_url(&u))
            .await
            .map_err(|e| e.to_string())??;
    }
    let client = dav_client();

    // The stored URL may already carry the home-set, or a principal to chase.
    let probe = dav_propfind(
        &client,
        url,
        username,
        password,
        dav_discovery_body(DavKind::Cal),
        "0",
    )
    .await?;
    let home = if let Some(h) = dav_home_set(&probe.body, DavKind::Cal) {
        Some(dav_resolve(&probe.url, &h))
    } else if let Some(principal) = dav_principal(&probe.body) {
        let purl = dav_resolve(&probe.url, &principal);
        let presp =
            dav_propfind(&client, &purl, username, password, dav_home_body(DavKind::Cal), "0")
                .await?;
        dav_home_set(&presp.body, DavKind::Cal).map(|h| dav_resolve(&presp.url, &h))
    } else {
        None
    };

    // Fallback: no discoverable home-set → the stored collection is the only one.
    let single = || {
        vec![CalendarInfo {
            id: url.to_string(),
            name: dav_last_segment(url),
            is_default: true,
            color: None,
        }]
    };
    let Some(home) = home else { return Ok(single()) };

    let enun =
        dav_propfind(&client, &home, username, password, dav_calendar_list_body(), "1").await?;
    if !dav_status_ok(enun.status) {
        return Err(format!("CalDAV server error: HTTP {}", enun.status));
    }
    let mut cals = dav_calendars(&enun.body);
    for c in &mut cals {
        c.id = dav_resolve(&enun.url, &c.id);
    }
    if cals.is_empty() {
        return Ok(single());
    }
    // Flag the stored collection as default (trailing-slash-insensitive); if none
    // match, the first enumerated calendar is the default.
    let norm = |s: &str| s.trim_end_matches('/').to_string();
    if let Some(m) = cals.iter_mut().find(|c| norm(&c.id) == norm(url)) {
        m.is_default = true;
    } else if let Some(first) = cals.first_mut() {
        first.is_default = true;
    }
    Ok(cals)
}

/// List an account's calendars [S-48], dispatched by provider. Mirrors
/// `get_calendar_events`' provider match. A 403 surfaces as
/// `calendar_not_authorized` so the UI can prompt for re-consent.
pub async fn list_calendars(
    app: &tauri::AppHandle,
    account_id: &str,
) -> Result<Vec<CalendarInfo>, String> {
    let accounts = load_accounts(app);
    let account = accounts
        .get(account_id)
        .ok_or_else(|| "Account not found".to_string())?
        .clone();

    match account.provider.as_str() {
        "caldav" => {
            let url = account.caldav_url.ok_or("No CalDAV URL configured")?;
            let username = account.username.unwrap_or_default();
            let password = account.password.unwrap_or_default();
            caldav_list_calendars(&url, &username, &password).await
        }
        "google" => {
            let token = get_valid_token(app, account_id).await?;
            google_calendars_core("https://www.googleapis.com", &token, &http_client()).await
        }
        _ => {
            let token = get_valid_token(app, account_id).await?;
            graph_calendars_core("https://graph.microsoft.com", &token, &http_client()).await
        }
    }
}

/// Append a `Z` to a Graph datetime ONLY when it carries no zone designator.
/// Graph (with `Prefer: outlook.timezone="UTC"`) returns naive UTC datetimes
/// like `2026-06-25T10:00:00`, which need a `Z` to parse as RFC3339. A string
/// that already ends in `Z` or a `±HH:MM` / `±HHMM` offset is already zoned:
/// blindly appending `Z` would yield `...-04:00Z`, an invalid datetime. This is
/// the exact bug that bit the frontend Google path (issue #48): a negative
/// offset has neither a trailing `Z` nor a `+`, so the old check appended a
/// stray `Z`. Mirrors `dates.ts` `gdt`/`hasZone`.
fn ensure_z(s: String) -> String {
    if dt_has_zone(&s) {
        s
    } else {
        s + "Z"
    }
}

/// True when the datetime's *time* portion ends in `Z` or a `±HH:MM` / `±HHMM`
/// offset. We scan only after the `T` so the date's own hyphens (`2026-06-25`)
/// are never mistaken for a negative offset. Mirrors the `dates.ts` `ZONE_RE`
/// anchoring on `str.slice(indexOf('T'))`.
fn dt_has_zone(s: &str) -> bool {
    let Some(t) = s.find('T') else { return false };
    let time = &s.as_bytes()[t..];
    if time.last() == Some(&b'Z') {
        return true;
    }
    // Trailing `±HH:MM` (6 bytes) or `±HHMM` (5 bytes): sign, then all digits
    // except an optional `:` in the colon form.
    let n = time.len();
    for width in [6usize, 5] {
        if n < width {
            continue;
        }
        let tail = &time[n - width..];
        if tail[0] != b'+' && tail[0] != b'-' {
            continue;
        }
        let digits_ok = tail[1..].iter().enumerate().all(|(i, &c)| {
            if width == 6 && i == 2 {
                c == b':'
            } else {
                c.is_ascii_digit()
            }
        });
        if digits_ok {
            return true;
        }
    }
    false
}

/// Fetch + paginate + parse Microsoft Graph calendar events. `base` is the
/// Graph origin (real: `https://graph.microsoft.com`; tests inject a mock URL).
/// Follows `@odata.nextLink` so busy calendars aren't silently truncated at the
/// first `$top=100` page (N1), capped at 100 pages so a malformed/looping
/// nextLink can never hang. No AppHandle / token-refresh here → unit-testable
/// with a mock server. Preserves the `Prefer: outlook.timezone="UTC"` header
/// and the `calendar_not_authorized` 403 mapping the UI depends on.
async fn graph_calendar_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
    start_dt: &str,
    end_dt: &str,
    calendar_id: Option<&str>,
) -> Result<Vec<CalendarEvent>, String> {
    // A specific calendar targets `/me/calendars/{id}/calendarView` [S-50];
    // absent → `/me/calendarView` (the account's default calendar, unchanged).
    // The id is opaque base64url and may carry `/` or `=`, so encode it.
    let scope = match calendar_id {
        Some(id) => format!("/me/calendars/{}/calendarView", urlencoding::encode(id)),
        None => "/me/calendarView".to_string(),
    };
    let mut url = format!(
        "{base}/v1.0{scope}\
         ?startDateTime={start_dt}&endDateTime={end_dt}\
         &$select=id,subject,start,end,isAllDay,location,organizer,webLink\
         &$orderby=start/dateTime\
         &$top=100"
    );
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp =
            get_with_retry(client, &url, token, Some("outlook.timezone=\"UTC\""))
                .await
                .map_err(|e| e.to_string())?;

        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "calendar_not_authorized".into()
            } else {
                format!("Graph API error {s}")
            });
        }

        let graph: GraphCalendarResp = resp.json().await.map_err(|e| e.to_string())?;

        for e in graph.value {
            result.push(CalendarEvent {
                id: e.id,
                subject: e.subject.unwrap_or_default(),
                start_dt: ensure_z(e.start.and_then(|d| d.date_time).unwrap_or_default()),
                end_dt: ensure_z(e.end.and_then(|d| d.date_time).unwrap_or_default()),
                is_all_day: e.is_all_day,
                location: e
                    .location
                    .and_then(|l| l.display_name)
                    .filter(|s| !s.is_empty()),
                organizer: e
                    .organizer
                    .and_then(|o| o.email_address)
                    .and_then(|a| a.name)
                    .filter(|s| !s.is_empty()),
                web_link: e.web_link.unwrap_or_default(),
            });
        }

        match graph.next_link {
            Some(next) if !next.is_empty() => url = next,
            _ => break,
        }
    }
    Ok(result)
}

#[derive(Deserialize)]
struct GraphCalendarListResp {
    value: Vec<GraphCalendarListEntry>,
    #[serde(rename = "@odata.nextLink", default)]
    next_link: Option<String>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphCalendarListEntry {
    id: String,
    name: Option<String>,
    #[serde(default)]
    is_default_calendar: bool,
    hex_color: Option<String>,
}

/// List the account's Microsoft Graph calendars [S-48]. Paginates
/// `@odata.nextLink` (cap 100 pages); a 403 surfaces as `calendar_not_authorized`
/// so the UI shows the same re-consent prompt as the events path. `base` is
/// injectable for mock-server unit tests.
async fn graph_calendars_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<CalendarInfo>, String> {
    let mut url =
        format!("{base}/v1.0/me/calendars?$select=id,name,isDefaultCalendar,hexColor&$top=100");
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = get_with_retry(client, &url, token, None)
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "calendar_not_authorized".into()
            } else {
                format!("Graph API error {s}")
            });
        }
        let data: GraphCalendarListResp = resp.json().await.map_err(|e| e.to_string())?;
        for c in data.value {
            result.push(CalendarInfo {
                id: c.id,
                name: c
                    .name
                    .filter(|s| !s.is_empty())
                    .unwrap_or_else(|| "Calendar".to_string()),
                is_default: c.is_default_calendar,
                color: normalize_cal_color(c.hex_color),
            });
        }
        match data.next_link {
            Some(next) if !next.is_empty() => url = next,
            _ => break,
        }
    }
    Ok(result)
}

/// Fetch + paginate + parse Google Calendar events. `base` is the API origin
/// (real: `https://www.googleapis.com`; tests inject a mock URL). Follows
/// `nextPageToken` so busy calendars aren't silently truncated at the first
/// `maxResults=100` page (N1), capped at 100 pages. No AppHandle /
/// token-refresh → unit-testable. Preserves the `calendar_not_authorized` 403
/// mapping the UI depends on.
async fn google_calendar_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
    start_dt: &str,
    end_dt: &str,
    calendar_id: Option<&str>,
) -> Result<Vec<CalendarEvent>, String> {
    #[derive(Deserialize)]
    struct GCalDate {
        #[serde(default)]
        date: Option<String>,
        #[serde(default, rename = "dateTime")]
        date_time: Option<String>,
    }
    #[derive(Deserialize)]
    struct GCalEvent {
        id: String,
        summary: Option<String>,
        location: Option<String>,
        #[serde(rename = "htmlLink")]
        html_link: Option<String>,
        start: Option<GCalDate>,
        end: Option<GCalDate>,
        #[serde(default)]
        organizer: Option<serde_json::Value>,
    }
    #[derive(Deserialize)]
    struct GCalResp {
        #[serde(default)]
        items: Vec<GCalEvent>,
        #[serde(rename = "nextPageToken", default)]
        next_page_token: Option<String>,
    }

    // Google accepts RFC3339 timestamps in `timeMin`/`timeMax`. Our inputs are
    // already ISO-8601 with offset (e.g. `2024-01-02T00:00:00Z`).
    // A specific calendar id targets that calendar [S-50]; absent → `primary`
    // (the account's default, unchanged). The id is email-like and may carry
    // `@`/`#`, so encode it into the path segment.
    let cal = calendar_id.map(urlencoding::encode).unwrap_or(std::borrow::Cow::Borrowed("primary"));
    let base_url = format!(
        "{base}/calendar/v3/calendars/{cal}/events\
         ?timeMin={}&timeMax={}\
         &singleEvents=true&orderBy=startTime&maxResults=100",
        urlencoding::encode(start_dt),
        urlencoding::encode(end_dt),
    );
    let mut url = base_url.clone();
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = client
            .get(&url)
            .bearer_auth(token)
            .send()
            .await
            .map_err(|e| e.to_string())?;

        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "calendar_not_authorized".into()
            } else {
                format!("Google Calendar API error {s}")
            });
        }

        let data: GCalResp = resp.json().await.map_err(|e| e.to_string())?;
        for e in data.items {
            let (start_dt_s, is_all_day) = match &e.start {
                Some(s) => match (&s.date_time, &s.date) {
                    (Some(dt), _) => (dt.clone(), false),
                    (None, Some(d)) => (format!("{d}T00:00:00Z"), true),
                    _ => (String::new(), false),
                },
                None => (String::new(), false),
            };
            let end_dt_s = match &e.end {
                Some(s) => match (&s.date_time, &s.date) {
                    (Some(dt), _) => dt.clone(),
                    (None, Some(d)) => format!("{d}T00:00:00Z"),
                    _ => String::new(),
                },
                None => String::new(),
            };
            let organizer = e
                .organizer
                .as_ref()
                .and_then(|v| {
                    v.get("displayName")
                        .and_then(|x| x.as_str())
                        .or_else(|| v.get("email").and_then(|x| x.as_str()))
                        .map(|s| s.to_string())
                })
                .filter(|s| !s.is_empty());

            result.push(CalendarEvent {
                id: e.id,
                subject: e.summary.unwrap_or_default(),
                start_dt: start_dt_s,
                end_dt: end_dt_s,
                is_all_day,
                location: e.location.filter(|s| !s.is_empty()),
                organizer,
                web_link: e.html_link.unwrap_or_default(),
            });
        }

        match data.next_page_token {
            Some(tok) if !tok.is_empty() => url = format!("{base_url}&pageToken={tok}"),
            _ => break,
        }
    }
    Ok(result)
}

/// List the account's Google calendars [S-48] via `calendarList`. Paginates
/// `nextPageToken` (cap 100 pages); a 403 surfaces as `calendar_not_authorized`.
/// `primary` marks the default; `backgroundColor` is the per-calendar hue.
async fn google_calendars_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<CalendarInfo>, String> {
    #[derive(Deserialize)]
    struct GCalListEntry {
        id: String,
        summary: Option<String>,
        #[serde(default)]
        primary: bool,
        #[serde(rename = "backgroundColor")]
        background_color: Option<String>,
    }
    #[derive(Deserialize)]
    struct GCalListResp {
        #[serde(default)]
        items: Vec<GCalListEntry>,
        #[serde(rename = "nextPageToken", default)]
        next_page_token: Option<String>,
    }

    let base_url = format!("{base}/calendar/v3/users/me/calendarList?maxResults=250");
    let mut url = base_url.clone();
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = client
            .get(&url)
            .bearer_auth(token)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "calendar_not_authorized".into()
            } else {
                format!("Google Calendar API error {s}")
            });
        }
        let data: GCalListResp = resp.json().await.map_err(|e| e.to_string())?;
        for c in data.items {
            let id = c.id;
            result.push(CalendarInfo {
                name: c
                    .summary
                    .filter(|s| !s.is_empty())
                    .unwrap_or_else(|| id.clone()),
                is_default: c.primary,
                color: normalize_cal_color(c.background_color),
                id,
            });
        }
        match data.next_page_token {
            Some(tok) if !tok.is_empty() => url = format!("{base_url}&pageToken={tok}"),
            _ => break,
        }
    }
    Ok(result)
}

// ---------------------------------------------------------------------------
// Contact birthdays
// ---------------------------------------------------------------------------

#[derive(Serialize, Clone)]
pub struct ContactBirthday {
    pub display_name: String,
    pub month: u32,
    pub day: u32,
    pub year: Option<i32>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphContactBday {
    display_name: Option<String>,
    birthday: Option<String>,
}

#[derive(Deserialize)]
struct GraphContactsBdayResp {
    value: Vec<GraphContactBday>,
    #[serde(rename = "@odata.nextLink", default)]
    next_link: Option<String>,
}

pub async fn get_contact_birthdays(
    app: &tauri::AppHandle,
    account_id: &str,
) -> Result<Vec<ContactBirthday>, String> {
    let accounts = load_accounts(app);
    let account = accounts
        .get(account_id)
        .ok_or_else(|| "Account not found".to_string())?
        .clone();

    // Google People API path
    if account.provider == "google" {
        let token = get_valid_token(app, account_id).await?;
        let client = http_client();
        return google_people_birthdays_core("https://people.googleapis.com", &token, &client)
            .await;
    }

    // Only Microsoft accounts have birthday support via Graph
    if account.provider != "microsoft" {
        return Ok(vec![]);
    }

    let token = get_valid_token(app, account_id).await?;
    graph_contact_birthdays_core("https://graph.microsoft.com", &token, &http_client()).await
}

/// Fetch + paginate + parse Google People contact birthdays. `base` is the
/// People API origin (real: `https://people.googleapis.com`; tests inject a
/// mock URL). Follows `nextPageToken` so accounts with >500 contacts aren't
/// silently truncated at the first `pageSize=500` page (N1), capped at 100
/// pages. No AppHandle / token-refresh → unit-testable. Preserves the
/// `contacts_not_authorized` 403 mapping the UI depends on.
async fn google_people_birthdays_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<ContactBirthday>, String> {
    #[derive(Deserialize)]
    struct Name {
        #[serde(rename = "displayName")]
        display_name: Option<String>,
    }
    #[derive(Deserialize)]
    struct BDay {
        #[serde(default)]
        date: Option<serde_json::Value>,
    }
    #[derive(Deserialize)]
    struct Person {
        #[serde(default)]
        names: Vec<Name>,
        #[serde(default)]
        birthdays: Vec<BDay>,
    }
    #[derive(Deserialize)]
    struct Conns {
        #[serde(default)]
        connections: Vec<Person>,
        #[serde(rename = "nextPageToken", default)]
        next_page_token: Option<String>,
    }

    let base_url =
        format!("{base}/v1/people/me/connections?personFields=names,birthdays&pageSize=500");
    let mut url = base_url.clone();
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = client
            .get(&url)
            .bearer_auth(token)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "contacts_not_authorized".into()
            } else {
                format!("People API error {s}")
            });
        }

        let data: Conns = resp.json().await.map_err(|e| e.to_string())?;
        for p in data.connections {
            let name = p
                .names
                .into_iter()
                .find_map(|n| n.display_name)
                .unwrap_or_default();
            if name.is_empty() {
                continue;
            }
            for b in p.birthdays {
                let Some(date) = b.date else { continue };
                let month = date.get("month").and_then(|v| v.as_u64()).unwrap_or(0) as u32;
                let day = date.get("day").and_then(|v| v.as_u64()).unwrap_or(0) as u32;
                if month == 0 || day == 0 {
                    continue;
                }
                let year = date.get("year").and_then(|v| v.as_i64()).map(|y| y as i32);
                result.push(ContactBirthday {
                    display_name: name.clone(),
                    month,
                    day,
                    year,
                });
                break;
            }
        }

        match data.next_page_token {
            Some(tok) if !tok.is_empty() => url = format!("{base_url}&pageToken={tok}"),
            _ => break,
        }
    }
    Ok(result)
}

/// Fetch + paginate + parse Microsoft Graph contact birthdays. `base` is the
/// Graph origin (real: `https://graph.microsoft.com`; tests inject a mock URL).
/// Follows `@odata.nextLink` so accounts with >500 contacts aren't silently
/// truncated (N1), capped at 100 pages so a malformed/looping nextLink can
/// never hang. No AppHandle / token-refresh here → unit-testable with a mock
/// server.
async fn graph_contact_birthdays_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<ContactBirthday>, String> {
    let mut url = format!(
        "{base}/v1.0/me/contacts?$select=displayName,birthday&$top=500&$orderby=displayName"
    );
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = get_with_retry(client, &url, token, None)
            .await
            .map_err(|e| e.to_string())?;

        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "contacts_not_authorized".into()
            } else {
                format!("Graph API error {s}")
            });
        }

        let data: GraphContactsBdayResp = resp.json().await.map_err(|e| e.to_string())?;
        for c in data.value {
            let (Some(name), Some(bday)) = (c.display_name, c.birthday) else {
                continue;
            };
            if bday.is_empty() {
                continue;
            }
            let date_part = bday.split('T').next().unwrap_or("");
            let segs: Vec<&str> = date_part.split('-').collect();
            if segs.len() != 3 {
                continue;
            }
            let year: i32 = segs[0].parse().unwrap_or(0);
            let month: u32 = segs[1].parse().unwrap_or(0);
            let day: u32 = segs[2].parse().unwrap_or(0);
            if month == 0 || day == 0 {
                continue;
            }
            result.push(ContactBirthday {
                display_name: name,
                month,
                day,
                year: if year > 1 { Some(year) } else { None },
            });
        }

        match data.next_link {
            Some(next) if !next.is_empty() => url = next,
            _ => break,
        }
    }
    Ok(result)
}

// ---------------------------------------------------------------------------
// Contacts
// ---------------------------------------------------------------------------

#[derive(Serialize, Clone)]
pub struct Contact {
    pub id: String,
    pub display_name: String,
    pub email: Option<String>,
    pub phone: Option<String>,
    pub job_title: Option<String>,
    pub company: Option<String>,
}

#[derive(Deserialize)]
struct GraphContactsResp {
    value: Vec<GraphContact>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphContact {
    id: String,
    display_name: Option<String>,
    email_addresses: Option<Vec<GraphEmailEntry>>,
    business_phones: Option<Vec<String>>,
    mobile_phone: Option<String>,
    job_title: Option<String>,
    company_name: Option<String>,
}

#[derive(Deserialize)]
struct GraphEmailEntry {
    address: Option<String>,
}

pub async fn get_contacts(
    app: &tauri::AppHandle,
    account_id: &str,
    search: &str,
    count: u32,
) -> Result<Vec<Contact>, String> {
    let accounts = load_accounts(app);
    let account = accounts
        .get(account_id)
        .ok_or_else(|| "Account not found".to_string())?
        .clone();

    match account.provider.as_str() {
        "carddav" => {
            let url = account.carddav_url.ok_or("No CardDAV URL configured")?;
            let username = account.username.unwrap_or_default();
            let password = account.password.unwrap_or_default();
            fetch_carddav_contacts(&url, &username, &password, search, count).await
        }
        "google" => {
            let token = get_valid_token(app, account_id).await?;
            let client = http_client();
            let url = format!(
                "https://people.googleapis.com/v1/people/me/connections\
                 ?personFields=names,emailAddresses,phoneNumbers,organizations\
                 &pageSize={}&sortOrder=FIRST_NAME_ASCENDING",
                count.min(1000)
            );
            let resp = client
                .get(&url)
                .bearer_auth(&token)
                .send()
                .await
                .map_err(|e| e.to_string())?;

            if !resp.status().is_success() {
                let s = resp.status().as_u16();
                let body = resp.text().await.unwrap_or_default();
                return Err(if s == 403 {
                    "contacts_not_authorized".into()
                } else {
                    format!("People API error {s}: {body}")
                });
            }

            #[derive(Deserialize)]
            struct Name {
                #[serde(rename = "displayName", default)]
                display_name: Option<String>,
            }
            #[derive(Deserialize)]
            struct Email {
                #[serde(default)]
                value: Option<String>,
            }
            #[derive(Deserialize)]
            struct Phone {
                #[serde(default)]
                value: Option<String>,
            }
            #[derive(Deserialize)]
            struct Org {
                #[serde(default)]
                name: Option<String>,
                #[serde(default)]
                title: Option<String>,
            }
            #[derive(Deserialize)]
            struct Person {
                #[serde(default, rename = "resourceName")]
                resource_name: Option<String>,
                #[serde(default)]
                names: Vec<Name>,
                #[serde(default, rename = "emailAddresses")]
                email_addresses: Vec<Email>,
                #[serde(default, rename = "phoneNumbers")]
                phone_numbers: Vec<Phone>,
                #[serde(default)]
                organizations: Vec<Org>,
            }
            #[derive(Deserialize)]
            struct Conns {
                #[serde(default)]
                connections: Vec<Person>,
            }

            let data: Conns = resp.json().await.map_err(|e| e.to_string())?;

            let needle = search.to_lowercase();
            let out: Vec<Contact> = data
                .connections
                .into_iter()
                .filter_map(|p| {
                    let display_name = p
                        .names
                        .into_iter()
                        .find_map(|n| n.display_name)
                        .unwrap_or_default();
                    if display_name.is_empty() {
                        return None;
                    }
                    if !needle.is_empty() && !display_name.to_lowercase().contains(&needle) {
                        return None;
                    }
                    let email = p
                        .email_addresses
                        .into_iter()
                        .find_map(|e| e.value)
                        .filter(|s| !s.is_empty());
                    let phone = p
                        .phone_numbers
                        .into_iter()
                        .find_map(|e| e.value)
                        .filter(|s| !s.is_empty());
                    let (job_title, company) = p
                        .organizations
                        .into_iter()
                        .next()
                        .map(|o| (o.title, o.name))
                        .unwrap_or((None, None));
                    Some(Contact {
                        id: p.resource_name.unwrap_or_default(),
                        display_name,
                        email,
                        phone,
                        job_title: job_title.filter(|s| !s.is_empty()),
                        company: company.filter(|s| !s.is_empty()),
                    })
                })
                .take(count as usize)
                .collect();
            Ok(out)
        }
        _ => {
            // Microsoft Graph
            let token = get_valid_token(app, account_id).await?;

            let mut url = format!(
                "https://graph.microsoft.com/v1.0/me/contacts\
                 ?$select=id,displayName,emailAddresses,businessPhones,mobilePhone,jobTitle,companyName\
                 &$top={count}"
            );

            if !search.is_empty() {
                let q = search.replace('\'', "''");
                url.push_str(&format!(
                    "&$filter=startswith(displayName,'{q}') or startswith(givenName,'{q}') or startswith(surname,'{q}')"
                ));
            } else {
                url.push_str("&$orderby=displayName");
            }

            let client = http_client();
            let resp = client
                .get(&url)
                .bearer_auth(&token)
                .send()
                .await
                .map_err(|e| e.to_string())?;

            if !resp.status().is_success() {
                let s = resp.status().as_u16();
                let body = resp.text().await.unwrap_or_default();
                return Err(if s == 403 {
                    "contacts_not_authorized".into()
                } else {
                    format!("Graph API error {s}: {body}")
                });
            }

            let graph: GraphContactsResp = resp.json().await.map_err(|e| e.to_string())?;

            Ok(graph
                .value
                .into_iter()
                .map(|c| {
                    let phone = c
                        .business_phones
                        .as_ref()
                        .and_then(|v| v.first())
                        .filter(|s| !s.is_empty())
                        .cloned()
                        .or_else(|| c.mobile_phone.filter(|s| !s.is_empty()));

                    Contact {
                        id: c.id,
                        display_name: c.display_name.unwrap_or_default(),
                        email: c
                            .email_addresses
                            .as_ref()
                            .and_then(|v| v.first())
                            .and_then(|e| e.address.clone())
                            .filter(|s| !s.is_empty()),
                        phone,
                        job_title: c.job_title.filter(|s| !s.is_empty()),
                        company: c.company_name.filter(|s| !s.is_empty()),
                    }
                })
                .collect())
        }
    }
}

// ---------------------------------------------------------------------------
// Microsoft To Do
// ---------------------------------------------------------------------------

#[derive(Serialize, Clone)]
pub struct TodoTaskList {
    pub id: String,
    pub display_name: String,
    pub is_default: bool,
}

#[derive(Serialize, Clone, Debug)]
pub struct TodoTask {
    pub id: String,
    pub title: String,
    pub status: String,
    pub importance: String,
    pub due_date: Option<String>,
    pub completed_at: Option<String>,
    pub list_id: String,
}

#[derive(Deserialize)]
struct GraphTodoListsResp {
    value: Vec<GraphTodoList>,
}

#[derive(Deserialize)]
struct GraphTodoList {
    id: String,
    #[serde(rename = "displayName")]
    display_name: Option<String>,
    #[serde(rename = "wellknownListName")]
    wellknown_list_name: Option<String>,
}

#[derive(Deserialize)]
struct GraphTodoTasksResp {
    value: Vec<GraphTodoTask>,
    #[serde(rename = "@odata.nextLink", default)]
    next_link: Option<String>,
}

#[derive(Deserialize)]
struct GraphTodoTask {
    id: String,
    title: Option<String>,
    status: Option<String>,
    importance: Option<String>,
    #[serde(rename = "dueDateTime")]
    due_date_time: Option<GraphDateTimeTz>,
    #[serde(rename = "completedDateTime")]
    completed_date_time: Option<GraphDateTimeTz>,
}

#[derive(Deserialize)]
struct GraphDateTimeTz {
    #[serde(rename = "dateTime")]
    date_time: Option<String>,
}

/// [S-52] Virtual list id for the cross-list "Today" view — not a real list on
/// either backend, so it is intercepted before any URL is built.
///
/// Microsoft To Do's own "My Day" is not reachable over an API: Graph exposes no
/// My Day property on `todoTask` (v1.0 or beta) and `wellknownListName` only has
/// `none | defaultList | flaggedEmails`; `/beta/me/planner/myDayTasks` returns
/// *Planner* tasks and is work-or-school only. Today therefore assembles the
/// half of that view the API does show — tasks due today, per Microsoft's own
/// definition — out of every list. Issue nwalser/themia-releases#80.
///
/// Kept in sync with `TODAY_LIST_ID` in `widgets/office/todo/todayList.ts`.
pub const TODAY_LIST_ID: &str = "__today__";

/// Bound on the Today fan-out: it reads every list, so a mailbox with an absurd
/// number of them can't turn one poll into hundreds of requests.
const TODAY_MAX_LISTS: usize = 50;

fn account_provider(app: &tauri::AppHandle, account_id: &str) -> String {
    load_accounts(app)
        .get(account_id)
        .map(|d| d.provider.clone())
        .unwrap_or_default()
}

/// Fetch + parse the Microsoft Graph To Do lists. `base` is the Graph origin
/// (tests inject a mock URL). No AppHandle / token-refresh → unit-testable.
async fn graph_todo_lists_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<TodoTaskList>, String> {
    let url = format!("{base}/v1.0/me/todo/lists");
    let resp = get_with_retry(client, &url, token, None)
        .await
        .map_err(|e| net_error("todo: graph lists", &e))?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(if s == 403 {
            "todo_not_authorized".into()
        } else {
            format!("Graph API error {s}")
        });
    }

    let graph: GraphTodoListsResp = resp.json().await.map_err(|e| e.to_string())?;

    Ok(graph
        .value
        .into_iter()
        .map(|l| TodoTaskList {
            id: l.id,
            display_name: l.display_name.unwrap_or_default(),
            is_default: l.wellknown_list_name.as_deref() == Some("defaultList"),
        })
        .collect())
}

/// Fetch + parse the Google Tasks lists. `base` is the Tasks API origin (tests
/// inject a mock URL). No AppHandle / token-refresh → unit-testable.
async fn google_todo_lists_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
) -> Result<Vec<TodoTaskList>, String> {
    let url = format!("{base}/tasks/v1/users/@me/lists");
    let resp = get_with_retry(client, &url, token, None)
        .await
        .map_err(|e| net_error("todo: google lists", &e))?;
    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(if s == 403 {
            "todo_not_authorized".into()
        } else {
            format!("Google Tasks API error {s}")
        });
    }
    #[derive(Deserialize)]
    struct GItem {
        id: String,
        title: Option<String>,
    }
    #[derive(Deserialize)]
    struct GResp {
        #[serde(default)]
        items: Vec<GItem>,
    }
    let data: GResp = resp.json().await.map_err(|e| e.to_string())?;
    Ok(data
        .items
        .into_iter()
        .enumerate()
        .map(|(i, l)| TodoTaskList {
            id: l.id,
            display_name: l.title.unwrap_or_default(),
            is_default: i == 0, // Google's "@default" is normally first; treat top as default.
        })
        .collect())
}

/// [S-52] Every task across every list, for the virtual Today view. The *day*
/// filter is not applied here: which calendar day a due value means is settled
/// on the frontend ([S-48], `todo/dueDate.ts`), and reimplementing that rule
/// here would fork it.
///
/// One list failing (a shared list that lost access, say) drops that list, not
/// the whole view — but if every list fails, the first error is returned rather
/// than an empty list that would read as "nothing due today".
async fn todo_today_core(
    lists: Vec<TodoTaskList>,
    mut fetch: impl FnMut(String) -> BoxFuture<Result<Vec<TodoTask>, String>>,
) -> Result<Vec<TodoTask>, String> {
    let mut result = Vec::new();
    let mut first_err: Option<String> = None;
    let mut attempted = 0usize;
    for list in lists.into_iter().take(TODAY_MAX_LISTS) {
        attempted += 1;
        match fetch(list.id.clone()).await {
            Ok(tasks) => result.extend(tasks),
            Err(e) => {
                log::warn!("todo: Today skipped list {}: {e}", list.id);
                if first_err.is_none() {
                    first_err = Some(e);
                }
            }
        }
    }
    match first_err {
        // Every list we tried failed → surface the failure instead of an empty
        // Today, which the widget would render as "nothing due".
        Some(e) if result.is_empty() && attempted > 0 => Err(e),
        _ => Ok(result),
    }
}

/// Boxed future alias so `todo_today_core` can take an async closure without a
/// generic future parameter per provider.
type BoxFuture<T> = std::pin::Pin<Box<dyn std::future::Future<Output = T> + Send>>;

pub async fn todo_get_lists(
    app: &tauri::AppHandle,
    account_id: &str,
) -> Result<Vec<TodoTaskList>, String> {
    let token = get_valid_token(app, account_id).await?;
    let client = http_client();
    if account_provider(app, account_id) == "google" {
        google_todo_lists_core("https://tasks.googleapis.com", &token, &client).await
    } else {
        graph_todo_lists_core("https://graph.microsoft.com", &token, &client).await
    }
}

pub async fn todo_get_tasks(
    app: &tauri::AppHandle,
    account_id: &str,
    list_id: &str,
    show_completed: bool,
) -> Result<Vec<TodoTask>, String> {
    let tasks = if account_provider(app, account_id) == "google" {
        let token = get_valid_token(app, account_id).await?;
        let client = http_client();
        const BASE: &str = "https://tasks.googleapis.com";
        if list_id == TODAY_LIST_ID {
            let lists = google_todo_lists_core(BASE, &token, &client).await?;
            let (token, client) = (token.clone(), client.clone());
            todo_today_core(lists, move |id| {
                let (token, client) = (token.clone(), client.clone());
                Box::pin(async move {
                    google_todo_tasks_core(BASE, &token, &client, &id, show_completed).await
                })
            })
            .await?
        } else {
            google_todo_tasks_core(BASE, &token, &client, list_id, show_completed).await?
        }
    } else {
        let token = get_valid_token(app, account_id).await?;
        let client = http_client();
        const BASE: &str = "https://graph.microsoft.com";
        if list_id == TODAY_LIST_ID {
            let lists = graph_todo_lists_core(BASE, &token, &client).await?;
            let (token, client) = (token.clone(), client.clone());
            todo_today_core(lists, move |id| {
                let (token, client) = (token.clone(), client.clone());
                Box::pin(async move { graph_todo_tasks_core(BASE, &token, &client, &id).await })
            })
            .await?
        } else {
            graph_todo_tasks_core(BASE, &token, &client, list_id).await?
        }
    };

    Ok(if show_completed {
        tasks
    } else {
        tasks
            .into_iter()
            .filter(|t| t.status != "completed")
            .collect()
    })
}

/// Fetch + paginate + parse Microsoft Graph To Do tasks for one list. `base` is
/// the Graph origin (tests inject a mock URL). Follows `@odata.nextLink` so long
/// task lists aren't truncated at the first `$top=200` page (N1), capped at 100
/// pages. No AppHandle / token-refresh → unit-testable. Returns the full list;
/// the caller applies any `show_completed` filtering.
async fn graph_todo_tasks_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
    list_id: &str,
) -> Result<Vec<TodoTask>, String> {
    let mut url = format!("{base}/v1.0/me/todo/lists/{list_id}/tasks?$top=200");
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = get_with_retry(client, &url, token, None)
            .await
            .map_err(|e| net_error("todo: graph tasks", &e))?;

        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "todo_not_authorized".into()
            } else {
                format!("Graph API error {s}")
            });
        }

        let graph: GraphTodoTasksResp = resp.json().await.map_err(|e| e.to_string())?;
        for t in graph.value {
            result.push(TodoTask {
                id: t.id,
                title: t.title.unwrap_or_default(),
                status: t.status.unwrap_or_else(|| "notStarted".into()),
                importance: t.importance.unwrap_or_else(|| "normal".into()),
                due_date: t.due_date_time.and_then(|d| d.date_time),
                completed_at: t.completed_date_time.and_then(|d| d.date_time),
                list_id: list_id.to_string(),
            });
        }

        match graph.next_link {
            Some(next) if !next.is_empty() => url = next,
            _ => break,
        }
    }
    Ok(result)
}

/// Fetch + paginate + parse Google Tasks for one list. `base` is the Tasks API
/// origin (tests inject a mock URL). Follows `nextPageToken` so long lists
/// aren't truncated at the first `maxResults=100` page (N1), capped at 100
/// pages. Statuses are mapped into the Microsoft To Do vocabulary the rest of
/// the app understands. Returns the full list; the caller filters completed.
async fn google_todo_tasks_core(
    base: &str,
    token: &str,
    client: &reqwest::Client,
    list_id: &str,
    show_completed: bool,
) -> Result<Vec<TodoTask>, String> {
    #[derive(Deserialize)]
    struct GTask {
        id: String,
        title: Option<String>,
        status: Option<String>, // "needsAction" | "completed"
        due: Option<String>,
        completed: Option<String>,
    }
    #[derive(Deserialize)]
    struct GResp {
        #[serde(default)]
        items: Vec<GTask>,
        #[serde(rename = "nextPageToken", default)]
        next_page_token: Option<String>,
    }

    let base_url = format!(
        "{base}/tasks/v1/lists/{list_id}/tasks\
         ?maxResults=100&showCompleted={}&showHidden=false",
        if show_completed { "true" } else { "false" }
    );
    let mut url = base_url.clone();
    let mut result = Vec::new();
    for _ in 0..100 {
        let resp = get_with_retry(client, &url, token, None)
            .await
            .map_err(|e| net_error("todo: google tasks", &e))?;
        if !resp.status().is_success() {
            let s = resp.status().as_u16();
            return Err(if s == 403 {
                "todo_not_authorized".into()
            } else {
                format!("Google Tasks API error {s}")
            });
        }
        let data: GResp = resp.json().await.map_err(|e| e.to_string())?;
        for t in data.items {
            result.push(TodoTask {
                id: t.id,
                title: t.title.unwrap_or_default(),
                // Map Google Tasks status → Microsoft To Do status vocabulary that
                // the rest of the app already understands ("completed" / "notStarted").
                status: match t.status.as_deref() {
                    Some("completed") => "completed".into(),
                    _ => "notStarted".into(),
                },
                importance: "normal".into(),
                due_date: t.due,
                completed_at: t.completed,
                list_id: list_id.to_string(),
            });
        }
        match data.next_page_token {
            Some(tok) if !tok.is_empty() => url = format!("{base_url}&pageToken={tok}"),
            _ => break,
        }
    }
    Ok(result)
}

pub async fn todo_update_task(
    app: &tauri::AppHandle,
    account_id: &str,
    list_id: &str,
    task_id: &str,
    status: &str,
) -> Result<(), String> {
    if account_provider(app, account_id) == "google" {
        let token = get_valid_token(app, account_id).await?;
        // Translate MS To Do status → Google Tasks status.
        let gstatus = if status == "completed" {
            "completed"
        } else {
            "needsAction"
        };
        let url = format!("https://tasks.googleapis.com/tasks/v1/lists/{list_id}/tasks/{task_id}");
        let mut body = serde_json::json!({ "status": gstatus });
        if gstatus == "needsAction" {
            // Clear completed timestamp so Google unmarks the task.
            body["completed"] = serde_json::Value::Null;
        }
        let client = http_client();
        let resp = client
            .patch(&url)
            .bearer_auth(&token)
            .json(&body)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            return Err(format!("Google Tasks API error {}", resp.status().as_u16()));
        }
        return Ok(());
    }

    let token = get_valid_token(app, account_id).await?;

    let client = http_client();
    let resp = client
        .patch(format!(
            "https://graph.microsoft.com/v1.0/me/todo/lists/{list_id}/tasks/{task_id}"
        ))
        .bearer_auth(&token)
        .header("Content-Type", "application/json")
        .body(format!(r#"{{"status":"{}"}}"#, status))
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(format!("Graph API error {s}"));
    }

    Ok(())
}

/// Rename a task. Both providers take a PATCH with just the new title, so this
/// only differs from `todo_update_task` in the field it sends — kept separate
/// rather than adding an optional-field mode, so neither call can accidentally
/// clear the other's field by omitting it.
pub async fn todo_rename_task(
    app: &tauri::AppHandle,
    account_id: &str,
    list_id: &str,
    task_id: &str,
    title: &str,
) -> Result<(), String> {
    let is_google = account_provider(app, account_id) == "google";
    let token = get_valid_token(app, account_id).await?;
    let url = if is_google {
        format!("https://tasks.googleapis.com/tasks/v1/lists/{list_id}/tasks/{task_id}")
    } else {
        format!("https://graph.microsoft.com/v1.0/me/todo/lists/{list_id}/tasks/{task_id}")
    };

    let client = http_client();
    let resp = client
        .patch(&url)
        .bearer_auth(&token)
        .json(&serde_json::json!({ "title": title }))
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(format!("API error {s}"));
    }

    Ok(())
}

pub async fn todo_delete_task(
    app: &tauri::AppHandle,
    account_id: &str,
    list_id: &str,
    task_id: &str,
) -> Result<(), String> {
    let is_google = account_provider(app, account_id) == "google";
    let token = get_valid_token(app, account_id).await?;
    let url = if is_google {
        format!("https://tasks.googleapis.com/tasks/v1/lists/{list_id}/tasks/{task_id}")
    } else {
        format!("https://graph.microsoft.com/v1.0/me/todo/lists/{list_id}/tasks/{task_id}")
    };

    let client = http_client();
    let resp = client
        .delete(&url)
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(format!("API error {s}"));
    }

    Ok(())
}

/// [S-52] A `YYYY-MM-DD` day, as the frontend writes it. Validated at the trust
/// boundary: anything else is dropped (the task is still created, just undated)
/// rather than interpolated into a request body.
fn valid_due_day(due_date: Option<&str>) -> Option<&str> {
    let day = due_date?;
    let ok = day.len() == 10
        && day.as_bytes()[4] == b'-'
        && day.as_bytes()[7] == b'-'
        && day
            .as_bytes()
            .iter()
            .enumerate()
            .all(|(i, c)| matches!(i, 4 | 7) || c.is_ascii_digit());
    if ok {
        Some(day)
    } else {
        log::warn!("todo: ignoring malformed due date {day:?} on create");
        None
    }
}

/// [S-52] The list a create lands in. Everything but the virtual Today id is
/// itself; Today has no list of its own, so its tasks are created in the
/// account's default list.
async fn todo_create_target_list(
    base: &str,
    token: &str,
    client: &reqwest::Client,
    list_id: &str,
    is_google: bool,
) -> Result<String, String> {
    if list_id != TODAY_LIST_ID {
        return Ok(list_id.to_string());
    }
    let lists = if is_google {
        google_todo_lists_core(base, token, client).await?
    } else {
        graph_todo_lists_core(base, token, client).await?
    };
    lists
        .iter()
        .find(|l| l.is_default)
        .or_else(|| lists.first())
        .map(|l| l.id.clone())
        .ok_or_else(|| "no_task_list".to_string())
}

pub async fn todo_create_task(
    app: &tauri::AppHandle,
    account_id: &str,
    list_id: &str,
    title: &str,
    due_date: Option<&str>,
) -> Result<TodoTask, String> {
    let due = valid_due_day(due_date);
    if account_provider(app, account_id) == "google" {
        let token = get_valid_token(app, account_id).await?;
        let client = http_client();
        const BASE: &str = "https://tasks.googleapis.com";
        let list_id = &todo_create_target_list(BASE, &token, &client, list_id, true).await?;
        let url = format!("{BASE}/tasks/v1/lists/{list_id}/tasks");
        let mut body = serde_json::json!({ "title": title });
        if let Some(day) = due {
            // Google documents `due` as a full RFC 3339 timestamp whose time
            // part is meaningless; UTC midnight is what its own clients write.
            body["due"] = serde_json::Value::String(format!("{day}T00:00:00.000Z"));
        }
        let resp = client
            .post(&url)
            .bearer_auth(&token)
            .json(&body)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !resp.status().is_success() {
            return Err(format!("Google Tasks API error {}", resp.status().as_u16()));
        }
        #[derive(Deserialize)]
        struct GTask {
            id: String,
            title: Option<String>,
            status: Option<String>,
            due: Option<String>,
            completed: Option<String>,
        }
        let t: GTask = resp.json().await.map_err(|e| e.to_string())?;
        return Ok(TodoTask {
            id: t.id,
            title: t.title.unwrap_or_default(),
            status: match t.status.as_deref() {
                Some("completed") => "completed".into(),
                _ => "notStarted".into(),
            },
            importance: "normal".into(),
            due_date: t.due,
            completed_at: t.completed,
            list_id: list_id.to_string(),
        });
    }

    let token = get_valid_token(app, account_id).await?;

    let client = http_client();
    const BASE: &str = "https://graph.microsoft.com";
    let list_id = &todo_create_target_list(BASE, &token, &client, list_id, false).await?;
    let mut body = serde_json::json!({ "title": title });
    if let Some(day) = due {
        // Midnight of the intended day, which is the encoding `dueDay` reads
        // back verbatim ([S-48]); the zone label is along for the ride.
        body["dueDateTime"] = serde_json::json!({
            "dateTime": format!("{day}T00:00:00.0000000"),
            "timeZone": "UTC",
        });
    }
    let resp = client
        .post(format!("{BASE}/v1.0/me/todo/lists/{list_id}/tasks"))
        .bearer_auth(&token)
        .json(&body)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        return Err(format!("Graph API error {s}"));
    }

    let task: GraphTodoTask = resp.json().await.map_err(|e| e.to_string())?;

    Ok(TodoTask {
        id: task.id,
        title: task.title.unwrap_or_default(),
        status: task.status.unwrap_or_else(|| "notStarted".into()),
        importance: task.importance.unwrap_or_else(|| "normal".into()),
        due_date: task.due_date_time.and_then(|d| d.date_time),
        completed_at: task.completed_date_time.and_then(|d| d.date_time),
        list_id: list_id.to_string(),
    })
}

// ── OneDrive ─────────────────────────────────────────────────────────────────

#[derive(Serialize, Clone)]
pub struct OneDriveItem {
    pub id: String,
    pub name: String,
    pub size: u64,
    pub last_modified: String,
    pub web_url: String,
    pub is_folder: bool,
    pub mime_type: Option<String>,
}

#[derive(Deserialize)]
struct GraphDriveItemsResponse {
    value: Vec<GraphDriveItem>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphDriveItem {
    id: Option<String>,
    name: Option<String>,
    size: Option<u64>,
    last_modified_date_time: Option<String>,
    web_url: Option<String>,
    folder: Option<serde_json::Value>,
    file: Option<GraphDriveFile>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphDriveFile {
    mime_type: Option<String>,
}

fn to_onedrive_item(g: &GraphDriveItem) -> OneDriveItem {
    OneDriveItem {
        id: g.id.clone().unwrap_or_default(),
        name: g.name.clone().unwrap_or_default(),
        size: g.size.unwrap_or(0),
        last_modified: g.last_modified_date_time.clone().unwrap_or_default(),
        web_url: g.web_url.clone().unwrap_or_default(),
        is_folder: g.folder.is_some(),
        mime_type: g.file.as_ref().and_then(|f| f.mime_type.clone()),
    }
}

pub async fn onedrive_recent_files(
    app: &tauri::AppHandle,
    account_id: &str,
    count: u32,
) -> Result<Vec<OneDriveItem>, String> {
    let token = get_valid_token(app, account_id).await?;
    let client = http_client();
    let resp = client
        .get(format!(
            "https://graph.microsoft.com/v1.0/me/drive/recent?$top={count}&$select=id,name,size,lastModifiedDateTime,webUrl,file,folder"
        ))
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        if s == 403 {
            return Err("not_authorized".into());
        }
        return Err(format!("Graph API error {s}"));
    }

    let data: GraphDriveItemsResponse = resp.json().await.map_err(|e| e.to_string())?;
    Ok(data.value.iter().map(to_onedrive_item).collect())
}

pub async fn onedrive_list_folder(
    app: &tauri::AppHandle,
    account_id: &str,
    folder_id: &str,
    count: u32,
) -> Result<Vec<OneDriveItem>, String> {
    let token = get_valid_token(app, account_id).await?;
    let client = http_client();

    let url = if folder_id.is_empty() || folder_id == "root" {
        format!(
            "https://graph.microsoft.com/v1.0/me/drive/root/children?$top={count}&$orderby=lastModifiedDateTime desc&$select=id,name,size,lastModifiedDateTime,webUrl,file,folder"
        )
    } else {
        format!(
            "https://graph.microsoft.com/v1.0/me/drive/items/{folder_id}/children?$top={count}&$orderby=lastModifiedDateTime desc&$select=id,name,size,lastModifiedDateTime,webUrl,file,folder"
        )
    };

    let resp = client
        .get(&url)
        .bearer_auth(&token)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !resp.status().is_success() {
        let s = resp.status().as_u16();
        if s == 403 {
            return Err("not_authorized".into());
        }
        return Err(format!("Graph API error {s}"));
    }

    let data: GraphDriveItemsResponse = resp.json().await.map_err(|e| e.to_string())?;
    Ok(data.value.iter().map(to_onedrive_item).collect())
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

#[cfg(test)]
mod imap_tests {
    use super::*;

    #[test]
    fn folder_name_mapping() {
        assert_eq!(imap_folder_name("inbox"), "INBOX");
        assert_eq!(imap_folder_name("sentitems"), "Sent");
        assert_eq!(imap_folder_name("deleteditems"), "Trash");
        assert_eq!(imap_folder_name("junkemail"), "Junk");
        assert_eq!(imap_folder_name("Custom"), "Custom");
    }

    #[test]
    fn header_parse_handles_folding() {
        let raw = "From: Alice <alice@example.com>\r\nSubject: hello\r\n world\r\nDate: Mon, 02 Jun 2025 10:00:00 +0000\r\n";
        assert_eq!(parse_header_field(raw, "subject"), "hello world");
        let (name, email) = parse_from_field(&parse_header_field(raw, "from"));
        assert_eq!(name, "Alice");
        assert_eq!(email, "alice@example.com");
    }

    #[test]
    fn folder_special_use_flags() {
        assert_eq!(folder_special_use("sentitems"), Some("\\Sent"));
        assert_eq!(folder_special_use("drafts"), Some("\\Drafts"));
        assert_eq!(folder_special_use("deleteditems"), Some("\\Trash"));
        assert_eq!(folder_special_use("junkemail"), Some("\\Junk"));
        assert_eq!(folder_special_use("archive"), Some("\\Archive"));
        assert_eq!(folder_special_use("inbox"), None);
    }

    #[test]
    fn folder_candidates_include_gmail_paths() {
        // Gmail nests its special folders under `[Gmail]/…`; the candidate
        // fallback must list them so a Gmail account resolves even if the
        // server somehow omits SPECIAL-USE attributes.
        assert!(folder_candidates("sentitems").contains(&"[Gmail]/Sent Mail"));
        assert!(folder_candidates("junkemail").contains(&"[Gmail]/Spam"));
        assert!(folder_candidates("archive").contains(&"[Gmail]/All Mail"));
        assert!(folder_candidates("inbox").is_empty());
    }

    // issue-45: the "most recent N" IMAP sequence-number range. A full mailbox
    // (thousands of messages) must yield the LAST `count` sequence numbers, not
    // an empty/inverted range. EXISTS is the highest sequence number; the newest
    // `count` messages are `[total-count+1 .. total]`, clamped to >= 1. An empty
    // mailbox yields None (nothing to FETCH). Pre-fix this math was inline in
    // fetch_imap_messages_sync and untested; pinning it guards the off-by-one /
    // inversion that would silently return zero messages.
    #[test]
    fn issue_45_recent_seq_range_returns_last_n() {
        // Full mailbox, want 10 most recent → 4991:5000.
        assert_eq!(recent_seq_range(5000, 10).as_deref(), Some("4991:5000"));
        // Fewer messages than requested → whole mailbox, never inverted.
        assert_eq!(recent_seq_range(3, 10).as_deref(), Some("1:3"));
        // Exactly `count` messages → 1:count.
        assert_eq!(recent_seq_range(10, 10).as_deref(), Some("1:10"));
        // A single message → just "1", not "1:1" edge that some servers dislike.
        assert_eq!(recent_seq_range(1, 10).as_deref(), Some("1"));
        // count == 1 on a full mailbox → only the newest (highest) seqno.
        assert_eq!(recent_seq_range(5000, 1).as_deref(), Some("5000"));
        // Empty mailbox → None (skip the FETCH entirely).
        assert_eq!(recent_seq_range(0, 10), None);
        // Defensive: count == 0 → None (asking for nothing).
        assert_eq!(recent_seq_range(5000, 0), None);
    }

    // ── CalDAV / CardDAV discovery parsers (RFC 6764) [S-45] ─────────────────

    #[test]
    fn s45_origin_and_resolve() {
        assert_eq!(
            dav_origin("https://cloud.example.com/remote.php/dav/"),
            Some("https://cloud.example.com".to_string())
        );
        assert_eq!(
            dav_origin("http://host.example:8443/x"),
            Some("http://host.example:8443".to_string())
        );
        assert_eq!(dav_origin("not-a-url"), None);

        // Absolute-path href resolves against the base's origin, not its path.
        assert_eq!(
            dav_resolve(
                "https://cloud.example.com/remote.php/dav/principals/users/alice/",
                "/remote.php/dav/calendars/alice/"
            ),
            "https://cloud.example.com/remote.php/dav/calendars/alice/"
        );
        // A full-URL href is taken as-is (cross-host possible).
        assert_eq!(
            dav_resolve("https://a.example/x", "https://b.example/y/"),
            "https://b.example/y/"
        );
        // A relative href joins to the base's directory.
        assert_eq!(
            dav_resolve("https://h.example/dav/cal/", "personal/"),
            "https://h.example/dav/cal/personal/"
        );
    }

    #[test]
    fn s45_prefix_insensitive_element_scan() {
        // Servers vary the namespace prefix freely; the local-name match must be
        // prefix- and case-insensitive.
        let xml = "<d:multistatus><D:current-user-principal><d:href>/p/alice/</d:href>\
            </D:current-user-principal></d:multistatus>";
        assert_eq!(dav_principal(xml).as_deref(), Some("/p/alice/"));

        // Entity-decoded + trimmed href.
        let amp = "<x><href>  /dav/a&amp;b/  </href></x>";
        assert_eq!(dav_href(amp).as_deref(), Some("/dav/a&b/"));

        // Local-name exactness: `calendar-proxy-read` must NOT match `calendar`.
        assert!(!dav_has("<C:calendar-proxy-read/>", "calendar"));
        assert!(dav_has("<C:calendar/>", "calendar"));
    }

    #[test]
    fn s45_home_set_extraction() {
        let cal = "<r><cal:calendar-home-set xmlns:cal=\"urn:ietf:params:xml:ns:caldav\">\
            <href>/remote.php/dav/calendars/alice/</href></cal:calendar-home-set></r>";
        assert_eq!(
            dav_home_set(cal, DavKind::Cal).as_deref(),
            Some("/remote.php/dav/calendars/alice/")
        );
        // Wrong kind: an addressbook home-set is not a calendar home-set.
        assert_eq!(dav_home_set(cal, DavKind::Card), None);

        let card = "<r><card:addressbook-home-set xmlns:card=\"urn:ietf:params:xml:ns:carddav\">\
            <href>/remote.php/dav/addressbooks/users/alice/</href></card:addressbook-home-set></r>";
        assert_eq!(
            dav_home_set(card, DavKind::Card).as_deref(),
            Some("/remote.php/dav/addressbooks/users/alice/")
        );
    }

    #[test]
    fn s45_collection_enumeration_filters_by_resourcetype_and_vevent() {
        // A Nextcloud-style Depth-1 home-set enumeration: the home container
        // itself (plain collection), a VEVENT calendar, a VTODO-only calendar,
        // and a birthday/subscription resource.
        let xml = r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
  <d:response><d:href>/dav/calendars/alice/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat>
  </d:response>
  <d:response><d:href>/dav/calendars/alice/personal/</d:href>
    <d:propstat><d:prop>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>
    </d:prop></d:propstat>
  </d:response>
  <d:response><d:href>/dav/calendars/alice/tasks/</d:href>
    <d:propstat><d:prop>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VTODO"/></c:supported-calendar-component-set>
    </d:prop></d:propstat>
  </d:response>
</d:multistatus>"#;
        // Only the VEVENT-capable calendar collection survives.
        assert_eq!(
            dav_collections(xml, DavKind::Cal),
            vec!["/dav/calendars/alice/personal/".to_string()]
        );

        let cards = r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:carddav">
  <d:response><d:href>/dav/addressbooks/users/alice/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat>
  </d:response>
  <d:response><d:href>/dav/addressbooks/users/alice/contacts/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/><c:addressbook/></d:resourcetype></d:prop></d:propstat>
  </d:response>
</d:multistatus>"#;
        assert_eq!(
            dav_collections(cards, DavKind::Card),
            vec!["/dav/addressbooks/users/alice/contacts/".to_string()]
        );
    }

    // [S-48] dav_calendars parses every VEVENT collection into a CalendarInfo:
    // displayname → name (last path segment when absent), calendar-color →
    // normalized color; a VTODO-only collection is skipped like dav_collections.
    #[test]
    fn s48_dav_calendars_parses_names_and_colors() {
        let xml = r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav" xmlns:x="http://apple.com/ns/ical/">
  <d:response><d:href>/dav/calendars/alice/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>
  <d:response><d:href>/dav/calendars/alice/personal/</d:href>
    <d:propstat><d:prop>
      <d:displayname>Personal</d:displayname>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>
      <x:calendar-color>#FF2968FF</x:calendar-color>
    </d:prop></d:propstat></d:response>
  <d:response><d:href>/dav/calendars/alice/work/</d:href>
    <d:propstat><d:prop>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>
    </d:prop></d:propstat></d:response>
  <d:response><d:href>/dav/calendars/alice/tasks/</d:href>
    <d:propstat><d:prop>
      <d:displayname>Tasks</d:displayname>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VTODO"/></c:supported-calendar-component-set>
    </d:prop></d:propstat></d:response>
</d:multistatus>"#;
        assert_eq!(
            dav_calendars(xml),
            vec![
                CalendarInfo {
                    id: "/dav/calendars/alice/personal/".into(),
                    name: "Personal".into(),
                    is_default: false,
                    color: Some("#ff2968".into()),
                },
                CalendarInfo {
                    // No displayname → last path segment.
                    id: "/dav/calendars/alice/work/".into(),
                    name: "work".into(),
                    is_default: false,
                    color: None,
                },
            ]
        );
    }

    #[test]
    fn s45_probe_is_collection_only_for_matching_kind() {
        let cal_collection = "<multistatus><response><href>/dav/calendars/alice/personal/</href>\
            <propstat><prop><resourcetype><collection/><C:calendar \
            xmlns:C=\"urn:ietf:params:xml:ns:caldav\"/></resourcetype></prop></propstat>\
            </response></multistatus>";
        assert!(dav_probe_is_collection(cal_collection, DavKind::Cal));
        assert!(!dav_probe_is_collection(cal_collection, DavKind::Card));

        // A bare DAV root (plain collection) is not a usable calendar.
        let root = "<multistatus><response><href>/remote.php/dav/</href>\
            <propstat><prop><resourcetype><collection/></resourcetype></prop></propstat>\
            </response></multistatus>";
        assert!(!dav_probe_is_collection(root, DavKind::Cal));
    }

    // Non-redirecting client mirroring the production `dav_client()`, so the
    // discovery code sees each 3xx and follows it itself (method-preserving).
    fn test_dav_client() -> reqwest::Client {
        reqwest::Client::builder()
            .redirect(reqwest::redirect::Policy::none())
            .build()
            .expect("build test dav client")
    }

    // #71 regression: on Nextcloud the *server root* rejects a basic-auth
    // PROPFIND with 401, so a user who pasted "https://cloud/" got "ERROR 401"
    // for CalDAV while their real CardDAV collection URL worked. Discovery must
    // fall back to `.well-known/caldav` (a 301 to the DAV context), then walk
    // current-user-principal -> calendar-home-set -> the calendar. Pre-fix the
    // add probe hit only "/" and returned 401.
    #[tokio::test]
    async fn s45_nextcloud_root_401_then_wellknown_discovers_calendar() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();

        let _root = server.mock("PROPFIND", "/").with_status(401).create_async().await;
        let _wk = server
            .mock("PROPFIND", "/.well-known/caldav")
            .with_status(301)
            .with_header("Location", "/remote.php/dav/")
            .create_async()
            .await;
        let _dav = server
            .mock("PROPFIND", "/remote.php/dav/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:"><d:response>
  <d:href>/remote.php/dav/</d:href>
  <d:propstat><d:prop>
    <d:resourcetype><d:collection/></d:resourcetype>
    <d:current-user-principal><d:href>/remote.php/dav/principals/users/alice/</d:href></d:current-user-principal>
  </d:prop></d:propstat>
</d:response></d:multistatus>"#,
            )
            .create_async()
            .await;
        let _principal = server
            .mock("PROPFIND", "/remote.php/dav/principals/users/alice/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav"><d:response>
  <d:href>/remote.php/dav/principals/users/alice/</d:href>
  <d:propstat><d:prop>
    <c:calendar-home-set><d:href>/remote.php/dav/calendars/alice/</d:href></c:calendar-home-set>
  </d:prop></d:propstat>
</d:response></d:multistatus>"#,
            )
            .create_async()
            .await;
        let _home = server
            .mock("PROPFIND", "/remote.php/dav/calendars/alice/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
  <d:response><d:href>/remote.php/dav/calendars/alice/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>
  <d:response><d:href>/remote.php/dav/calendars/alice/personal/</d:href>
    <d:propstat><d:prop>
      <d:resourcetype><d:collection/><c:calendar/></d:resourcetype>
      <c:supported-calendar-component-set><c:comp name="VEVENT"/></c:supported-calendar-component-set>
    </d:prop></d:propstat></d:response>
</d:multistatus>"#,
            )
            .create_async()
            .await;

        let entered = format!("{base}/");
        let got = dav_discover_collection(&test_dav_client(), &entered, "alice", "pw", DavKind::Cal)
            .await
            .expect("discovery should resolve the calendar via .well-known");
        assert_eq!(got, format!("{base}/remote.php/dav/calendars/alice/personal/"));
    }

    // An exact collection URL is used verbatim — one round-trip, no principal
    // walk (backward-compatible with pre-discovery accounts).
    #[tokio::test]
    async fn s45_exact_collection_url_used_verbatim() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _m = server
            .mock("PROPFIND", "/dav/calendars/alice/personal/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav"><d:response>
  <d:href>/dav/calendars/alice/personal/</d:href>
  <d:propstat><d:prop><d:resourcetype><d:collection/><c:calendar/></d:resourcetype></d:prop></d:propstat>
</d:response></d:multistatus>"#,
            )
            .create_async()
            .await;

        let entered = format!("{base}/dav/calendars/alice/personal/");
        let got = dav_discover_collection(&test_dav_client(), &entered, "alice", "pw", DavKind::Cal)
            .await
            .expect("exact collection URL should be accepted");
        assert_eq!(got, entered);
    }

    // CardDAV discovery without a redirect: principal -> addressbook-home-set ->
    // the contacts address book.
    #[tokio::test]
    async fn s45_carddav_discovers_addressbook_via_principal() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _dav = server
            .mock("PROPFIND", "/dav/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:"><d:response>
  <d:href>/dav/</d:href>
  <d:propstat><d:prop>
    <d:resourcetype><d:collection/></d:resourcetype>
    <d:current-user-principal><d:href>/dav/principals/users/bob/</d:href></d:current-user-principal>
  </d:prop></d:propstat>
</d:response></d:multistatus>"#,
            )
            .create_async()
            .await;
        let _principal = server
            .mock("PROPFIND", "/dav/principals/users/bob/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:carddav"><d:response>
  <d:href>/dav/principals/users/bob/</d:href>
  <d:propstat><d:prop>
    <c:addressbook-home-set><d:href>/dav/addressbooks/users/bob/</d:href></c:addressbook-home-set>
  </d:prop></d:propstat>
</d:response></d:multistatus>"#,
            )
            .create_async()
            .await;
        let _home = server
            .mock("PROPFIND", "/dav/addressbooks/users/bob/")
            .with_status(207)
            .with_body(
                r#"<d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:carddav">
  <d:response><d:href>/dav/addressbooks/users/bob/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop></d:propstat></d:response>
  <d:response><d:href>/dav/addressbooks/users/bob/contacts/</d:href>
    <d:propstat><d:prop><d:resourcetype><d:collection/><c:addressbook/></d:resourcetype></d:prop></d:propstat></d:response>
</d:multistatus>"#,
            )
            .create_async()
            .await;

        let entered = format!("{base}/dav/");
        let got = dav_discover_collection(&test_dav_client(), &entered, "bob", "pw", DavKind::Card)
            .await
            .expect("carddav discovery should resolve the address book");
        assert_eq!(got, format!("{base}/dav/addressbooks/users/bob/contacts/"));
    }

    // Genuinely wrong credentials: every candidate 401s, so the error still
    // surfaces the 401 (discovery must not mask a real auth failure).
    #[tokio::test]
    async fn s45_all_candidates_401_reports_auth_failure() {
        let mut server = mockito::Server::new_async().await;
        let base = server.url();
        let _root = server.mock("PROPFIND", "/").with_status(401).create_async().await;
        let _wk = server
            .mock("PROPFIND", "/.well-known/caldav")
            .with_status(401)
            .create_async()
            .await;

        let entered = format!("{base}/");
        let err = dav_discover_collection(&test_dav_client(), &entered, "alice", "bad", DavKind::Cal)
            .await
            .expect_err("wrong credentials must fail");
        assert!(err.contains("401"), "error should mention 401, got: {err}");
    }

    // ── CalDAV / CardDAV pure parsers ───────────────────────────────────────

    #[test]
    fn caldav_dt_roundtrip() {
        assert_eq!(caldav_dt_to_iso("20260401T120000Z"), "2026-04-01T12:00:00Z");
        assert_eq!(caldav_dt_to_iso("20260401"), "2026-04-01"); // all-day
        assert_eq!(iso_to_caldav_dt("2026-04-01T00:00:00Z"), "20260401T000000Z");
    }

    // issue #66: a TZID-local DATE-TIME must convert to a true UTC instant. The
    // old path stripped `TZID=` and emitted the naive wall-clock time, which the
    // frontend read as UTC → a GMT-4 (EDT) event rendered 4h early.
    #[test]
    fn s66_caldav_tzid_to_utc() {
        // 2026-04-01 is EDT (UTC-4): 12:00 New York → 16:00Z.
        assert_eq!(
            caldav_dt_to_iso_tz("20260401T120000", Some("America/New_York")),
            "2026-04-01T16:00:00Z",
        );
        // Winter date is EST (UTC-5): 12:00 New York → 17:00Z.
        assert_eq!(
            caldav_dt_to_iso_tz("20260115T120000", Some("America/New_York")),
            "2026-01-15T17:00:00Z",
        );
        // A trailing Z always wins (already UTC); the TZID is ignored.
        assert_eq!(
            caldav_dt_to_iso_tz("20260401T120000Z", Some("America/New_York")),
            "2026-04-01T12:00:00Z",
        );
        // VALUE=DATE all-day (no TZID, no time) is returned verbatim.
        assert_eq!(caldav_dt_to_iso_tz("20260605", None), "2026-06-05");
        // Floating time (no zone) keeps the prior naive best-effort (no Z).
        assert_eq!(
            caldav_dt_to_iso_tz("20260401T120000", None),
            "2026-04-01T12:00:00",
        );
        // An unknown zone must not panic; falls back to floating.
        assert_eq!(
            caldav_dt_to_iso_tz("20260401T120000", Some("Mars/Olympus_Mons")),
            "2026-04-01T12:00:00",
        );
    }

    // ical_tzid: pull the TZID parameter out of a full property line (case- and
    // quote-insensitive), or None when absent.
    #[test]
    fn s66_ical_tzid_extraction() {
        assert_eq!(
            ical_tzid("DTSTART;TZID=America/New_York:20260401T120000").as_deref(),
            Some("America/New_York"),
        );
        // Case-insensitive parameter name, quoted value.
        assert_eq!(
            ical_tzid("DTSTART;tzid=\"Europe/Zurich\":20260401T120000").as_deref(),
            Some("Europe/Zurich"),
        );
        // No TZID present (UTC value / all-day).
        assert_eq!(ical_tzid("DTSTART:20260401T120000Z"), None);
        assert_eq!(ical_tzid("DTSTART;VALUE=DATE:20260605"), None);
    }

    // issue #66 (end-to-end through the parser): a zoned VEVENT resolves to UTC.
    #[test]
    fn s66_parse_ics_events_zoned_to_utc() {
        let xml = "<D:multistatus xmlns:D=\"DAV:\"><D:response><C:calendar-data>\
BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:tz-1@example.com\r\n\
SUMMARY:NY Meeting\r\nDTSTART;TZID=America/New_York:20260401T120000\r\n\
DTEND;TZID=America/New_York:20260401T130000\r\n\
END:VEVENT\r\nEND:VCALENDAR\
</C:calendar-data></D:response></D:multistatus>";
        let events = parse_ics_events(xml);
        assert_eq!(events.len(), 1);
        let e = &events[0];
        assert_eq!(e.start_dt, "2026-04-01T16:00:00Z");
        assert_eq!(e.end_dt, "2026-04-01T17:00:00Z");
        assert!(!e.is_all_day);
    }

    #[test]
    fn parse_ics_events_extracts_fields() {
        // A REPORT multistatus carrying one VEVENT inside <calendar-data>.
        let xml = "<D:multistatus xmlns:D=\"DAV:\"><D:response><C:calendar-data>\
BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:evt-1@example.com\r\n\
SUMMARY:Team Sync\r\nDTSTART:20260401T090000Z\r\nDTEND:20260401T093000Z\r\n\
LOCATION:Room 4\r\nORGANIZER;CN=\"Alice Boss\":mailto:alice@example.com\r\n\
END:VEVENT\r\nEND:VCALENDAR\
</C:calendar-data></D:response></D:multistatus>";
        let events = parse_ics_events(xml);
        assert_eq!(events.len(), 1);
        let e = &events[0];
        assert_eq!(e.id, "evt-1@example.com");
        assert_eq!(e.subject, "Team Sync");
        assert_eq!(e.start_dt, "2026-04-01T09:00:00Z");
        assert_eq!(e.end_dt, "2026-04-01T09:30:00Z");
        assert!(!e.is_all_day);
        assert_eq!(e.location.as_deref(), Some("Room 4"));
        assert_eq!(e.organizer.as_deref(), Some("Alice Boss"));
    }

    // 1.21 / N5-N6: all-day events carry an EXCLUSIVE DTEND (RFC 5545): a
    // single-day all-day event on Jun 5 is DTSTART;VALUE=DATE:20260605 /
    // DTEND;VALUE=DATE:20260606. The parser must preserve that end VERBATIM (no
    // -1 adjustment): the frontend renderer (Calendar.tsx `dayEvents`) treats the
    // span as half-open [start, end), so the exclusive end already renders the
    // event on Jun 5 only. Subtracting a day here, the tempting "fix", would
    // make a one-day all-day event VANISH. This test guards against that.
    #[test]
    fn parse_ics_all_day_preserves_exclusive_end() {
        let xml = "<D:multistatus xmlns:D=\"DAV:\"><D:response><C:calendar-data>\
BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:allday-1@example.com\r\n\
SUMMARY:Conference\r\nDTSTART;VALUE=DATE:20260605\r\nDTEND;VALUE=DATE:20260606\r\n\
END:VEVENT\r\nEND:VCALENDAR\
</C:calendar-data></D:response></D:multistatus>";
        let events = parse_ics_events(xml);
        assert_eq!(events.len(), 1);
        let e = &events[0];
        assert!(e.is_all_day, "VALUE=DATE event must be flagged all-day");
        assert_eq!(e.start_dt, "2026-06-05");
        assert_eq!(e.end_dt, "2026-06-06", "exclusive DTEND preserved verbatim");
    }

    // Multi-day all-day (Jun 5-7 inclusive) → DTEND;VALUE=DATE:20260608 (exclusive).
    // End preserved as 06-08 so the half-open renderer spans 5, 6, 7.
    #[test]
    fn parse_ics_multi_day_all_day_preserves_exclusive_end() {
        let xml = "<D:multistatus xmlns:D=\"DAV:\"><D:response><C:calendar-data>\
BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:allday-3@example.com\r\n\
SUMMARY:Trip\r\nDTSTART;VALUE=DATE:20260605\r\nDTEND;VALUE=DATE:20260608\r\n\
END:VEVENT\r\nEND:VCALENDAR\
</C:calendar-data></D:response></D:multistatus>";
        let events = parse_ics_events(xml);
        assert_eq!(events.len(), 1);
        let e = &events[0];
        assert!(e.is_all_day);
        assert_eq!(e.start_dt, "2026-06-05");
        assert_eq!(e.end_dt, "2026-06-08");
    }

    // issue-48: `ensure_z` must only append `Z` to a genuinely zoneless Graph
    // datetime. The old check (`ends_with('Z') || contains('+')`) missed
    // negative offsets: `2026-06-25T10:00:00-04:00` has neither, so it became
    // `...-04:00Z`, an invalid datetime. Unreachable today (Graph sends
    // `Prefer: outlook.timezone="UTC"` → naive UTC) but a latent foot-gun if the
    // header changes or another provider reuses this helper. Mirrors the
    // frontend regression in calendar/dates.test.ts [S-41].
    #[test]
    fn issue_48_ensure_z_only_zones_naive_datetimes() {
        // Already zoned → preserved verbatim, no stray Z.
        assert_eq!(
            ensure_z("2026-06-25T10:00:00Z".into()),
            "2026-06-25T10:00:00Z"
        );
        assert_eq!(
            ensure_z("2026-06-25T10:00:00+02:00".into()),
            "2026-06-25T10:00:00+02:00"
        );
        assert_eq!(
            ensure_z("2026-06-25T10:00:00-04:00".into()),
            "2026-06-25T10:00:00-04:00"
        );
        // Naive / zoneless → `Z` appended so it parses as UTC.
        assert_eq!(
            ensure_z("2026-06-25T10:00:00".into()),
            "2026-06-25T10:00:00Z"
        );
        // Edge cases mirroring dates.ts `hasZone`: colon-less offset, half-hour
        // offset, and an empty string (no `T` → treated as zoneless, but stays
        // untouched-ish. Graph never emits this; documents the contract).
        assert_eq!(
            ensure_z("2026-06-25T10:00:00+0200".into()),
            "2026-06-25T10:00:00+0200"
        );
        assert_eq!(
            ensure_z("2026-06-25T10:00:00-02:30".into()),
            "2026-06-25T10:00:00-02:30"
        );
        assert_eq!(ensure_z("".into()), "Z");
    }

    #[test]
    fn parse_vcards_extracts_fields_and_skips_empty_fn() {
        let xml = "<D:multistatus xmlns:D=\"DAV:\"><D:response><C:address-data>\
BEGIN:VCARD\r\nVERSION:3.0\r\nUID:c-1\r\nFN:Jane Doe\r\n\
EMAIL;TYPE=INTERNET:jane@example.com\r\nTEL:+1-555-0100\r\n\
ORG:Acme Inc;Sales\r\nTITLE:Engineer\r\nEND:VCARD\
</C:address-data></D:response>\
<D:response><C:address-data>BEGIN:VCARD\r\nVERSION:3.0\r\nEND:VCARD</C:address-data></D:response>\
</D:multistatus>";
        let contacts = parse_vcards(xml);
        assert_eq!(contacts.len(), 1, "VCARD without FN must be skipped");
        let c = &contacts[0];
        assert_eq!(c.display_name, "Jane Doe");
        assert_eq!(c.email.as_deref(), Some("jane@example.com"));
        assert_eq!(c.phone.as_deref(), Some("+1-555-0100"));
        assert_eq!(c.company.as_deref(), Some("Acme Inc"));
        assert_eq!(c.job_title.as_deref(), Some("Engineer"));
    }

    // 1.21: a malformed feed must never panic the parser. Before the fix,
    // caldav_dt_to_iso byte-sliced the date prefix and a multi-byte char would
    // cause a mid-character slice panic.
    #[test]
    fn caldav_dt_to_iso_tolerates_non_ascii_and_short() {
        // Multi-byte chars in the date prefix, must not panic, returns input.
        assert_eq!(caldav_dt_to_iso("20€60401"), "20€60401");
        assert_eq!(caldav_dt_to_iso("héllo"), "héllo");
        assert_eq!(caldav_dt_to_iso(""), "");
        assert_eq!(caldav_dt_to_iso("2026"), "2026");
        // A well-formed date still parses.
        assert_eq!(caldav_dt_to_iso("20260401"), "2026-04-01");
    }

    // 1.21: a non-ASCII organizer name (CN= with accents) must not panic. Before
    // the fix, the byte offset came from a to_uppercase() copy whose length can
    // differ from the original, indexing mid-character.
    #[test]
    fn parse_ics_events_non_ascii_organizer_no_panic() {
        let ics = "BEGIN:VEVENT\r\nUID:x\r\nSUMMARY:S\r\nDTSTART:20260401T090000Z\r\n\
ORGANIZER;CN=\"Renée Straße Müller\":mailto:r@x.com\r\nEND:VEVENT";
        let events = parse_ics_events(ics);
        assert_eq!(events.len(), 1);
        assert_eq!(events[0].organizer.as_deref(), Some("Renée Straße Müller"));
    }

    // A feed <title> preceded by multi-byte chars whose lowercase has a
    // different byte length must not panic and must extract the title. Before
    // the fix, parse_feed_title indexed `body` with offsets taken from
    // `body.to_lowercase()`, which could land mid-character and panic.
    #[test]
    fn parse_feed_title_tolerates_non_ascii_and_extracts() {
        // `İ` (U+0130) lowercases to two bytes, shifting offsets in a folded copy.
        assert_eq!(
            parse_feed_title("<rss><channel><title>Hello İ Feed</title></channel></rss>")
                .as_deref(),
            Some("Hello İ Feed"),
        );
        // Case-insensitive opening/closing tags still match.
        assert_eq!(
            parse_feed_title("<TITLE>Mixed</TITLE>").as_deref(),
            Some("Mixed"),
        );
        // CDATA wrapper is stripped.
        assert_eq!(
            parse_feed_title("<title><![CDATA[ Wrapped ]]></title>").as_deref(),
            Some("Wrapped"),
        );
        // Adversarial / truncated inputs return None without panicking.
        assert_eq!(parse_feed_title(""), None);
        assert_eq!(parse_feed_title("<title"), None);
        assert_eq!(parse_feed_title("<title>"), None);
        assert_eq!(parse_feed_title("<title></title>"), None);
        assert_eq!(parse_feed_title("日本語 <title> </title>"), None);
        assert_eq!(parse_feed_title("İİİ<title>X"), None);
    }

    #[test]
    fn find_ci_ascii_indexes_into_original() {
        // Case-insensitive match, index valid for the ORIGINAL (note the leading
        // multi-byte char that would shift offsets in a case-folded copy).
        let s = "Ωcn=Bob";
        let i = find_ci_ascii(s, "CN=").unwrap();
        assert_eq!(&s[i..i + 3], "cn=");
        assert_eq!(&s[i + 3..], "Bob");
        assert!(find_ci_ascii("nothing here", "CN=").is_none());
        assert!(find_ci_ascii("x", "longer").is_none());
    }

    // 1.21: fuzz-style sweep, adversarial / truncated / non-ASCII blobs through
    // every hand-rolled parser. The assertion is simply "returns without
    // panicking"; a panic here fails the test.
    #[test]
    fn parsers_never_panic_on_adversarial_input() {
        let inputs = [
            "",
            "BEGIN:VEVENT",
            "BEGIN:VEVENT\r\nEND:VEVENT",
            "BEGIN:VEVENT\nDTSTART:20€\nEND:VEVENT",
            "BEGIN:VEVENT\nDTSTART:T\nDTEND:\nORGANIZER;CN=\nEND:VEVENT",
            "BEGIN:VCARD\r\nFN:\r\nEND:VCARD",
            "BEGIN:VCARD\nFN:Ωmega\nEMAIL:\nORG:;\nEND:VCARD",
            "BEGIN:VEVENT\r\nORGANIZER:mailto:\r\nEND:VEVENT",
            ":::::",
            "=?UTF-8?B?",
            "=?x?Q?=ZZ=\r\n",
            "héllo wörld 日本語 \u{0}\u{1}",
        ];
        for s in inputs {
            let _ = parse_ics_events(s);
            let _ = parse_vcards(s);
            let _ = unfold_ical(s);
            let _ = caldav_dt_to_iso(s);
            let _ = get_ical_prop(s, "DTSTART");
            let _ = get_ical_line(s, "ORGANIZER");
            let _ = decode_qp(s);
            let _ = parse_rfc2822_date(s);
            let _ = parse_from_field(s);
        }
    }

    // A malformed query pair (stray `&`, valueless param) must be skipped, not
    // abort the whole redirect parse: code/state are still recovered.
    #[test]
    fn parse_oauth_redirect_skips_malformed_pairs() {
        assert_eq!(
            parse_oauth_redirect("GET /?code=abc&state=xyz HTTP/1.1"),
            Some(("abc".to_string(), "xyz".to_string())),
        );
        // A bare `&`, a valueless `foo`, and an unknown key all skipped.
        assert_eq!(
            parse_oauth_redirect("GET /?&foo&bar=1&code=abc&state=xyz HTTP/1.1"),
            Some(("abc".to_string(), "xyz".to_string())),
        );
        // error param surfaces with the __ERROR__ marker.
        let (c, _) = parse_oauth_redirect("GET /?error=access_denied&state=s HTTP/1.1").unwrap();
        assert!(c.starts_with("__ERROR__"));
        // no query at all → None.
        assert_eq!(parse_oauth_redirect("GET / HTTP/1.1"), None);
    }

    // The account id itself contains colons for Google (google:sub) and IMAP
    // (imap:user@host). split_message_id must recover the FULL account id via the
    // LAST colon: splitting on the first one returned the wrong id and broke
    // mark/delete/move for those providers ("Account not found").
    #[test]
    fn split_message_id_recovers_colon_account_ids() {
        assert_eq!(
            split_message_id("48d3-guid:AAMkABC=="),
            Ok(("48d3-guid", "AAMkABC=="))
        );
        assert_eq!(
            split_message_id("google:117xyz:18f0hex"),
            Ok(("google:117xyz", "18f0hex"))
        );
        assert_eq!(
            split_message_id("imap:bob@mail.example:sentitems|42"),
            Ok(("imap:bob@mail.example", "sentitems|42")),
        );
        assert!(split_message_id("no-separator").is_err());
    }

    // IMAP raw id encodes `folder|uid|uidvalidity` so an action targets the
    // displayed mailbox (not a hardcoded INBOX) and can refuse if that mailbox
    // was recreated. Older shapes (`folder|uid`, bare `uid`) stay supported so a
    // mid-cycle upgrade is never worse than before.
    #[test]
    fn imap_folder_uid_validity_parses_all_shapes() {
        // Three-part: folder + uid + uidvalidity.
        assert_eq!(
            imap_folder_uid_validity("inbox|12|1"),
            Ok(("inbox", 12, Some(1)))
        );
        assert_eq!(
            imap_folder_uid_validity("sentitems|42|7788"),
            Ok(("sentitems", 42, Some(7788)))
        );
        // Two-part legacy: folder + uid, validity unknown → guard skipped.
        assert_eq!(
            imap_folder_uid_validity("deleteditems|7"),
            Ok(("deleteditems", 7, None))
        );
        // One-part legacy: bare uid defaults to inbox.
        assert_eq!(imap_folder_uid_validity("99"), Ok(("inbox", 99, None)));
        // Malformed uid / uidvalidity are rejected, not silently coerced.
        assert!(imap_folder_uid_validity("sentitems|notanumber").is_err());
        assert!(imap_folder_uid_validity("notanumber").is_err());
        assert!(imap_folder_uid_validity("inbox|12|notanumber").is_err());
    }

    // The UIDVALIDITY guard fires ONLY on a known mismatch. A matching value, an
    // id with no captured validity (legacy), or a server that omits UIDVALIDITY
    // all proceed — so the guard never regresses a working mark/delete/move.
    #[test]
    fn ensure_uid_validity_only_blocks_on_known_mismatch() {
        let mbx = |v: Option<u32>| imap::types::Mailbox {
            uid_validity: v,
            ..Default::default()
        };
        // Both known and equal → proceed.
        assert!(ensure_uid_validity(&mbx(Some(1)), Some(1), "delete").is_ok());
        // Both known and different → refuse.
        assert!(ensure_uid_validity(&mbx(Some(2)), Some(1), "delete").is_err());
        // Id carried no validity (legacy two-part id) → proceed.
        assert!(ensure_uid_validity(&mbx(Some(9)), None, "delete").is_ok());
        // Server omitted UIDVALIDITY → unverifiable, proceed (never worse).
        assert!(ensure_uid_validity(&mbx(None), Some(1), "delete").is_ok());
    }

    // Live tests run against a real IMAP server (GreenMail/Dovecot or the
    // bundled Python test server). Set the env vars and pass `--ignored`:
    //   IMAP_TEST_HOST=localhost IMAP_TEST_PORT=3993 IMAP_TEST_USER=test@localhost \
    //   IMAP_TEST_PASS=secret cargo test -p widget-email imap_live -- --ignored --nocapture

    fn live_params() -> (String, u16, String, String) {
        (
            std::env::var("IMAP_TEST_HOST").expect("IMAP_TEST_HOST"),
            std::env::var("IMAP_TEST_PORT")
                .unwrap_or_else(|_| "993".into())
                .parse()
                .unwrap(),
            std::env::var("IMAP_TEST_USER").expect("IMAP_TEST_USER"),
            std::env::var("IMAP_TEST_PASS").expect("IMAP_TEST_PASS"),
        )
    }

    #[test]
    #[ignore]
    fn imap_live_fetch() {
        let (host, port, user, pass) = live_params();
        let msgs = fetch_imap_messages_sync(
            host,
            port,
            user.clone(),
            pass,
            "inbox".into(),
            10,
            format!("imap:{}", user),
            user.clone(),
            String::new(),
            "imap".into(),
        )
        .expect("fetch should succeed");

        println!("Fetched {} messages from INBOX", msgs.len());
        for m in &msgs {
            println!(
                "  [{}] {} | from {} <{}> | read={} | {}",
                m.id, m.subject, m.from_name, m.from_email, m.is_read, m.received_at
            );
        }
        assert!(!msgs.is_empty(), "INBOX should have at least one message");
    }

    /// Verifies SPECIAL-USE folder resolution against a live server that
    /// advertises Gmail-style `[Gmail]/…` mailboxes. Confirms a logical
    /// `sentitems` resolves to the `\Sent`-tagged mailbox, not the bare `Sent`.
    #[test]
    #[ignore]
    fn imap_live_resolve_folders() {
        let (host, port, user, pass) = live_params();
        let tls = native_tls::TlsConnector::builder().build().unwrap();
        let client = imap::connect((host.as_str(), port), host.as_str(), &tls).unwrap();
        let mut session = client.login(&user, &pass).map_err(|(e, _)| e).unwrap();

        for (logical, expect) in [
            ("inbox", "INBOX"),
            ("sentitems", "[Gmail]/Sent Mail"),
            ("drafts", "[Gmail]/Drafts"),
            ("deleteditems", "[Gmail]/Trash"),
            ("junkemail", "[Gmail]/Spam"),
        ] {
            let got = resolve_imap_mailbox(&mut session, logical);
            println!("resolve({logical:?}) -> {got:?}");
            assert_eq!(got, expect, "folder {logical} should resolve to {expect}");
        }
        session.logout().ok();
    }

    // Live DAV tests run against the bundled Python dav_server.py (or any real
    // CalDAV/CardDAV server). Set DAV_TEST_URL to the collection URL:
    //   DAV_TEST_URL=http://localhost:8008/cal DAV_TEST_USER=u DAV_TEST_PASS=p \
    //   cargo test -p widget-email dav_live -- --ignored --nocapture

    fn dav_rt() -> tokio::runtime::Runtime {
        tokio::runtime::Builder::new_current_thread()
            .enable_all()
            .build()
            .unwrap()
    }

    #[test]
    #[ignore]
    fn dav_live_caldav_fetch() {
        let url = std::env::var("DAV_TEST_URL").expect("DAV_TEST_URL");
        let user = std::env::var("DAV_TEST_USER").unwrap_or_else(|_| "u".into());
        let pass = std::env::var("DAV_TEST_PASS").unwrap_or_else(|_| "p".into());

        let events = dav_rt()
            .block_on(fetch_caldav_events(
                &url,
                &user,
                &pass,
                "2026-01-01T00:00:00Z",
                "2026-12-31T00:00:00Z",
            ))
            .expect("CalDAV fetch should succeed");

        println!("Fetched {} event(s)", events.len());
        for e in &events {
            println!(
                "  {} | {} | {} -> {} | loc={:?} | org={:?}",
                e.id, e.subject, e.start_dt, e.end_dt, e.location, e.organizer
            );
        }
        assert!(
            !events.is_empty(),
            "calendar should return at least one event"
        );
    }

    #[test]
    #[ignore]
    fn dav_live_carddav_fetch() {
        let url = std::env::var("DAV_TEST_URL").expect("DAV_TEST_URL");
        let user = std::env::var("DAV_TEST_USER").unwrap_or_else(|_| "u".into());
        let pass = std::env::var("DAV_TEST_PASS").unwrap_or_else(|_| "p".into());

        let contacts = dav_rt()
            .block_on(fetch_carddav_contacts(&url, &user, &pass, "", 50))
            .expect("CardDAV fetch should succeed");

        println!("Fetched {} contact(s)", contacts.len());
        for c in &contacts {
            println!(
                "  {} | {} | email={:?} | tel={:?} | org={:?}",
                c.id, c.display_name, c.email, c.phone, c.company
            );
        }
        assert!(
            !contacts.is_empty(),
            "address book should return at least one contact"
        );
    }

    // -----------------------------------------------------------------------
    // [S-38] Automated IMAP fetch integration coverage (issue #45).
    //
    // Runs the REAL post-login session/select/fetch/parse path
    // (`fetch_imap_messages_from_session`) against an in-process mock IMAP
    // server over a plaintext `TcpStream`: no Docker, no JVM, no network, no
    // env vars, not `#[ignore]`. Production uses implicit TLS (`imap::connect`
    // + native_tls); CLAUDE.md forbids any cert-validation bypass, so the test
    // exercises the session logic over plaintext instead (the `imap` crate's
    // `Client::new` wraps any `Read + Write`). This does NOT cover the TLS
    // handshake itself, only everything after a connected stream.
    // -----------------------------------------------------------------------

    use std::io::{BufRead, BufReader, Read as _, Write};
    use std::net::{TcpListener, TcpStream};

    /// One seeded message. Sequence number is its 1-based position in the list.
    #[derive(Clone)]
    struct MockMsg {
        uid: u32,
        seen: bool,
        header: String,
        /// Text body served as the `BODY[TEXT]` section (issue #45b preview).
        body: String,
    }

    /// How the mock server should behave for a single client connection.
    #[derive(Clone)]
    struct MockConfig {
        msgs: Vec<MockMsg>,
        /// Reply to SELECT with a tagged NO (simulates an inaccessible folder).
        fail_select: bool,
        /// Reply to FETCH with a tagged NO (simulates a mid-fetch failure).
        fail_fetch: bool,
        /// UIDVALIDITY reported in the SELECT response. Ids minted with a
        /// different value must be refused by `ensure_uid_validity`.
        uid_validity: u32,
    }

    impl Default for MockConfig {
        fn default() -> Self {
            // UIDVALIDITY defaults to 1 so a freshly-fetched id (also 1) matches.
            Self {
                msgs: Vec::new(),
                fail_select: false,
                fail_fetch: false,
                uid_validity: 1,
            }
        }
    }

    /// Bind to 127.0.0.1:0, spawn a thread that handles exactly ONE plaintext
    /// client connection, then return the OS-assigned port. The dialog is
    /// lenient: it reads CRLF-terminated command lines, keys off the verb (the
    /// second whitespace token, uppercased) regardless of argument ordering, and
    /// echoes the client's tag (first token) in every tagged response.
    fn start_mock_imap(cfg: MockConfig) -> u16 {
        let listener = TcpListener::bind("127.0.0.1:0").expect("bind ephemeral port");
        let port = listener.local_addr().expect("local_addr").port();

        std::thread::spawn(move || {
            let (stream, _) = listener.accept().expect("accept one client");
            handle_client(stream, &cfg);
        });

        port
    }

    fn write_line(w: &mut impl Write, s: &str) {
        w.write_all(s.as_bytes()).expect("write response");
        w.write_all(b"\r\n").expect("write CRLF");
    }

    /// Expand an IMAP sequence set ("12" or "3:12") into concrete seq numbers.
    fn expand_seq_set(set: &str, max_seq: usize) -> Vec<usize> {
        if let Some((a, b)) = set.split_once(':') {
            let lo: usize = a.trim().parse().unwrap_or(1);
            let hi: usize = b.trim().parse().unwrap_or(max_seq);
            (lo..=hi).collect()
        } else {
            set.trim()
                .parse::<usize>()
                .map(|n| vec![n])
                .unwrap_or_default()
        }
    }

    fn handle_client(stream: TcpStream, cfg: &MockConfig) {
        let mut writer = stream.try_clone().expect("clone stream for writing");
        let mut reader = BufReader::new(stream);

        // Greeting: `imap::Client::read_greeting` waits for this untagged line.
        write_line(&mut writer, "* OK [CAPABILITY IMAP4rev1] mock IMAP ready");

        let mut line = String::new();
        loop {
            line.clear();
            let n = reader.read_line(&mut line).unwrap_or(0);
            if n == 0 {
                break; // client closed
            }
            let trimmed = line.trim_end_matches(['\r', '\n']);
            let mut tokens = trimmed.split_whitespace();
            let tag = tokens.next().unwrap_or("*").to_string();
            let mut verb = tokens.next().unwrap_or("").to_ascii_uppercase();
            // Normalize "UID FETCH" → treat as FETCH; the sequence set then
            // shifts one token to the right, which expand_seq_set handles.
            let is_uid = verb == "UID";
            if is_uid {
                verb = tokens.next().unwrap_or("").to_ascii_uppercase();
            }

            match verb.as_str() {
                "CAPABILITY" => {
                    write_line(&mut writer, "* CAPABILITY IMAP4rev1");
                    write_line(&mut writer, &format!("{tag} OK CAPABILITY completed"));
                }
                "LOGIN" => {
                    write_line(&mut writer, &format!("{tag} OK LOGIN completed"));
                }
                "LIST" | "LSUB" => {
                    // Empty list: resolve_imap_mailbox falls back to the
                    // requested name. (Unused on the INBOX path, but answered
                    // for leniency.)
                    write_line(&mut writer, &format!("{tag} OK {verb} completed"));
                }
                "SELECT" | "EXAMINE" => {
                    if cfg.fail_select {
                        write_line(&mut writer, &format!("{tag} NO mailbox unavailable"));
                    } else {
                        write_line(&mut writer, &format!("* {} EXISTS", cfg.msgs.len()));
                        write_line(&mut writer, "* 0 RECENT");
                        write_line(
                            &mut writer,
                            &format!("* OK [UIDVALIDITY {}] UIDs valid", cfg.uid_validity),
                        );
                        write_line(
                            &mut writer,
                            &format!("{tag} OK [READ-WRITE] SELECT completed"),
                        );
                    }
                }
                "FETCH" => {
                    if cfg.fail_fetch {
                        write_line(&mut writer, &format!("{tag} NO fetch failed"));
                    } else {
                        // Sequence set is the token right after the verb.
                        let set = tokens.next().unwrap_or("");
                        // Remaining tokens are the fetch-att spec. Per RFC 3501
                        // (fetch = ... fetch-att / "(" fetch-att *(SP fetch-att) ")")
                        // a MULTI-att list MUST be parenthesized; a single att may
                        // be bare. A strict server (Dovecot) rejects the bare
                        // multi-att form with "Invalid arguments" — the exact
                        // issue-#45 failure. Enforce it here so the in-process
                        // test faithfully reproduces the strict-server path
                        // instead of the lenient Gmail/Outlook behavior.
                        let atts: Vec<&str> = tokens.collect();
                        let parenthesized = atts.first().is_some_and(|t| t.starts_with('('))
                            && atts.last().is_some_and(|t| t.ends_with(')'));
                        if atts.len() > 1 && !parenthesized {
                            write_line(
                                &mut writer,
                                &format!("{tag} NO Error in IMAP command FETCH: Invalid arguments"),
                            );
                            continue;
                        }
                        for seq in expand_seq_set(set, cfg.msgs.len()) {
                            if seq == 0 || seq > cfg.msgs.len() {
                                continue;
                            }
                            let m = &cfg.msgs[seq - 1];
                            let flags = if m.seen { "(\\Seen)" } else { "()" };
                            let hbytes = m.header.as_bytes();
                            let bbytes = m.body.as_bytes();
                            // RFC822.HEADER and BODY[TEXT] as IMAP literals:
                            // {len}CRLF then the raw bytes, both inside the FETCH
                            // parenthesized list. The leading space before
                            // BODY[TEXT] separates the two sections (issue #45b).
                            write_line(
                                &mut writer,
                                &format!(
                                    "* {seq} FETCH (UID {uid} FLAGS {flags} RFC822.HEADER {{{len}}}",
                                    uid = m.uid,
                                    len = hbytes.len(),
                                ),
                            );
                            writer.write_all(hbytes).expect("write header literal");
                            write_line(
                                &mut writer,
                                &format!(" BODY[TEXT] {{{len}}}", len = bbytes.len()),
                            );
                            writer.write_all(bbytes).expect("write body literal");
                            write_line(&mut writer, ")");
                        }
                        write_line(&mut writer, &format!("{tag} OK FETCH completed"));
                    }
                }
                "STORE" => {
                    // `UID STORE <uid> <+/-FLAGS (...)>`. Per RFC 3501 a STORE
                    // flag list may be bare OR parenthesized, so no strict parens
                    // check here — just acknowledge (optionally echoing a FETCH).
                    write_line(&mut writer, &format!("{tag} OK STORE completed"));
                }
                "EXPUNGE" => {
                    // `UID EXPUNGE <uid>` (UIDPLUS). Acknowledge.
                    write_line(&mut writer, &format!("{tag} OK EXPUNGE completed"));
                }
                "MOVE" => {
                    // `UID MOVE <uid> <mailbox>` (RFC 6851). Acknowledge.
                    write_line(&mut writer, &format!("{tag} OK MOVE completed"));
                }
                "STATUS" => {
                    // `STATUS <mailbox> (att att ...)`. The att list MUST be
                    // parenthesized (RFC 3501); reject a bare list like FETCH so
                    // the mock faithfully models a strict server.
                    let _mailbox = tokens.next();
                    let atts: Vec<&str> = tokens.collect();
                    let parenthesized = atts.first().is_some_and(|t| t.starts_with('('))
                        && atts.last().is_some_and(|t| t.ends_with(')'));
                    if !parenthesized {
                        write_line(
                            &mut writer,
                            &format!("{tag} NO Error in IMAP command STATUS: Invalid arguments"),
                        );
                        continue;
                    }
                    let unseen = cfg.msgs.iter().filter(|m| !m.seen).count();
                    write_line(
                        &mut writer,
                        &format!(
                            "* STATUS INBOX (MESSAGES {} UNSEEN {})",
                            cfg.msgs.len(),
                            unseen
                        ),
                    );
                    write_line(&mut writer, &format!("{tag} OK STATUS completed"));
                }
                "LOGOUT" => {
                    write_line(&mut writer, "* BYE logging out");
                    write_line(&mut writer, &format!("{tag} OK LOGOUT completed"));
                    break;
                }
                "NOOP" => {
                    write_line(&mut writer, &format!("{tag} OK NOOP completed"));
                }
                _ => {
                    // Lenient: acknowledge anything else so the session proceeds.
                    write_line(&mut writer, &format!("{tag} OK completed"));
                }
            }
        }

        // Drain any trailing bytes so the client's writes don't RST.
        let _ = reader.get_mut().read(&mut [0u8; 0]);
    }

    /// Connect a plaintext client to the mock and log in, mirroring what
    /// production does after `imap::connect` (which reads the greeting + builds
    /// a `Client`) but over a `TcpStream` instead of a TLS stream.
    fn connect_and_login(port: u16) -> imap::Session<TcpStream> {
        let stream = TcpStream::connect(("127.0.0.1", port)).expect("connect to mock");
        let mut client = imap::Client::new(stream);
        client.read_greeting().expect("read greeting");
        client
            .login("user", "pass")
            .map_err(|(e, _)| e)
            .expect("login")
    }

    fn header(subject: &str, from: &str, date: &str) -> String {
        format!("From: {from}\r\nSubject: {subject}\r\nDate: {date}\r\nMessage-ID: <x>\r\n\r\n")
    }

    /// issue #45: happy path, 12 messages exist, request 10, assert newest-first
    /// order, parsed header fields, and \Seen-derived is_read.
    #[test]
    fn issue_45_imap_fetch_against_local_server() {
        // Seq 1..=12, UID == seq for clarity. Seq 12 is newest; mark a couple
        // \Seen so is_read can be asserted both ways.
        let msgs: Vec<MockMsg> = (1..=12)
            .map(|n| MockMsg {
                uid: n,
                seen: n % 2 == 0, // even seqs read, odd unread
                header: header(
                    &format!("Subject {n}"),
                    &format!("\"Sender {n}\" <sender{n}@example.com>"),
                    "Mon, 01 Jun 2026 12:34:56 +0000",
                ),
                body: format!("Body of message {n}."),
            })
            .collect();

        let port = start_mock_imap(MockConfig {
            msgs,
            ..Default::default()
        });
        let mut session = connect_and_login(port);

        let out = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "user@example.com",
            "Nick",
            "imap",
        )
        .expect("fetch should succeed");
        session.logout().ok();

        // recent_seq_range(12, 10) = "3:12" → 10 messages.
        assert_eq!(out.len(), 10, "should return exactly the requested count");

        // Newest-first: seq/uid 12 first, seq/uid 3 last.
        assert_eq!(out[0].subject, "Subject 12");
        assert_eq!(out[9].subject, "Subject 3");
        // Ids encode `folder|uid|uidvalidity`; the mock reports UIDVALIDITY 1.
        // Round-trip through the real parser rather than string-matching.
        let raw0 = split_message_id(&out[0].id).expect("id splits").1;
        assert_eq!(imap_folder_uid_validity(raw0), Ok(("inbox", 12, Some(1))));
        let raw9 = split_message_id(&out[9].id).expect("id splits").1;
        assert_eq!(imap_folder_uid_validity(raw9), Ok(("inbox", 3, Some(1))));

        // Parsed header fields on the newest message.
        assert_eq!(out[0].from_name, "Sender 12");
        assert_eq!(out[0].from_email, "sender12@example.com");
        assert_eq!(out[0].received_at, "2026-06-01T12:34:56Z");

        // is_read mirrors the \Seen flag (seq 12 even → seen; seq 3 odd → unseen).
        assert!(out[0].is_read, "seq 12 was \\Seen");
        assert!(!out[9].is_read, "seq 3 was not \\Seen");

        // Account fields propagate.
        assert_eq!(out[0].account_id, "imap:acct");
        assert_eq!(out[0].account_email, "user@example.com");
        assert_eq!(out[0].account_nickname, "Nick");
        assert_eq!(out[0].account_provider, "imap");
    }

    /// issue #45 (real root cause): a strict server (Dovecot) rejects an
    /// UNPARENTHESIZED multi-att FETCH — `FETCH 1:3 RFC822.HEADER FLAGS UID` —
    /// with `Error in IMAP command FETCH: Invalid arguments`, while lenient
    /// servers (Gmail/Outlook) accept it. The mock now enforces the RFC 3501
    /// grammar, so this fails (fetch returns Err) unless production issues the
    /// PARENTHESIZED form `(RFC822.HEADER FLAGS UID)`. Focused guard on the
    /// exact regression the reporter hit.
    #[test]
    fn issue_45_imap_fetch_uses_parenthesized_att_list() {
        let msgs: Vec<MockMsg> = (1..=3)
            .map(|n| MockMsg {
                uid: n,
                seen: false,
                header: header("S", "a@b.com", "Mon, 01 Jun 2026 00:00:00 +0000"),
                body: "b".into(),
            })
            .collect();

        let port = start_mock_imap(MockConfig {
            msgs,
            ..Default::default()
        });
        let mut session = connect_and_login(port);

        let out = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "u@e.com",
            "",
            "imap",
        )
        .expect("strict server must accept a parenthesized FETCH att list");
        session.logout().ok();

        assert_eq!(
            out.len(),
            3,
            "all 3 messages fetched via a parenthesized att list"
        );
    }

    /// issue #45: regression guard for the original bug, a failed fetch must
    /// surface as `Err(_)`, never a silent `Ok(vec![])`.
    #[test]
    fn issue_45_imap_fetch_error_not_swallowed() {
        // SELECT succeeds (EXISTS=5) but FETCH returns a tagged NO.
        let msgs: Vec<MockMsg> = (1..=5)
            .map(|n| MockMsg {
                uid: n,
                seen: false,
                header: header("S", "a@b.com", "Mon, 01 Jun 2026 00:00:00 +0000"),
                body: "b".into(),
            })
            .collect();

        let port = start_mock_imap(MockConfig {
            msgs,
            fail_fetch: true,
            ..Default::default()
        });
        let mut session = connect_and_login(port);

        let res = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "u@e.com",
            "",
            "imap",
        );

        assert!(
            res.is_err(),
            "a failed FETCH must be an Err, not Ok(empty); returned {} messages",
            res.map(|v| v.len()).unwrap_or(0),
        );
    }

    /// issue #45: a genuinely empty mailbox (EXISTS=0) is success, distinct
    /// from the error case above. No FETCH is issued when total == 0.
    #[test]
    fn issue_45_imap_fetch_empty_mailbox_is_ok() {
        let port = start_mock_imap(MockConfig::default()); // zero messages
        let mut session = connect_and_login(port);

        let out = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "u@e.com",
            "",
            "imap",
        )
        .expect("empty mailbox is a successful, empty fetch");
        session.logout().ok();

        assert!(out.is_empty(), "EXISTS=0 yields an empty vec, not an error");
    }

    /// issue #45(b): IMAP messages used to hardcode `preview: ""`. The fetch now
    /// pulls BODY[TEXT] and populates a plaintext snippet (HTML stripped,
    /// whitespace collapsed).
    #[test]
    fn issue_45_imap_fetch_populates_preview() {
        let msgs = vec![MockMsg {
            uid: 1,
            seen: false,
            header: header("Hi", "a@b.com", "Mon, 01 Jun 2026 00:00:00 +0000"),
            body: "<p>Hello   <b>world</b>,\r\nthis  is the body.</p>".into(),
        }];
        let port = start_mock_imap(MockConfig {
            msgs,
            ..Default::default()
        });
        let mut session = connect_and_login(port);

        let out = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "u@e.com",
            "",
            "imap",
        )
        .expect("fetch should succeed");
        session.logout().ok();

        assert_eq!(out.len(), 1);
        assert_eq!(
            out[0].preview, "Hello world, this is the body.",
            "preview is the HTML-stripped, whitespace-collapsed body text",
        );
    }

    /// issue #114: end to end over the mock server — a real multipart message
    /// (preamble, boundaries, per-part headers, quoted-printable) must preview
    /// as the sender's text. Before the fix this read "This is a multi-part
    /// message in MIME format. --bnd Content-Type: text/plain; …".
    #[test]
    fn issue_114_imap_fetch_previews_multipart_text_part() {
        let header = "From: a@b.com\r\nSubject: Hi\r\nDate: Mon, 01 Jun 2026 00:00:00 +0000\r\n\
             Content-Type: multipart/alternative; boundary=\"bnd42\"\r\n\r\n"
            .to_string();
        let body = "This is a multi-part message in MIME format.\r\n\
             --bnd42\r\n\
             Content-Type: text/plain; charset=utf-8; format=flowed\r\n\
             Content-Transfer-Encoding: quoted-printable\r\n\r\n\
             Your caf=C3=A9 order is ready.\r\n\
             --bnd42\r\n\
             Content-Type: text/html; charset=utf-8\r\n\r\n\
             <html><body><p>Your caf&eacute; order is ready.</p></body></html>\r\n\
             --bnd42--\r\n"
            .to_string();

        let port = start_mock_imap(MockConfig {
            msgs: vec![MockMsg {
                uid: 1,
                seen: false,
                header,
                body,
            }],
            ..Default::default()
        });
        let mut session = connect_and_login(port);

        let out = fetch_imap_messages_from_session(
            &mut session,
            "inbox",
            10,
            "imap:acct",
            "u@e.com",
            "",
            "imap",
        )
        .expect("fetch should succeed");
        session.logout().ok();

        assert_eq!(out.len(), 1);
        assert_eq!(
            out[0].preview, "Your caf\u{e9} order is ready.",
            "preview is the decoded text/plain part, with no MIME scaffolding",
        );
    }

    // -----------------------------------------------------------------------
    // Mutation coverage (mark-read / delete / move / STATUS). The mock used to
    // rubber-stamp every non-SELECT/FETCH verb via its catch-all, so these
    // paths had ZERO automated coverage — the exact blind spot that hid #45.
    // Now the mock answers STORE/EXPUNGE/MOVE/STATUS explicitly (STATUS strictly,
    // rejecting a bare att list), so the real select/guard/command path runs.
    // -----------------------------------------------------------------------

    /// Three unread messages (uid == seq), the fixture for the mutation tests.
    fn three_msgs() -> Vec<MockMsg> {
        (1..=3)
            .map(|n| MockMsg {
                uid: n,
                seen: false,
                header: header("S", "a@b.com", "Mon, 01 Jun 2026 00:00:00 +0000"),
                body: "b".into(),
            })
            .collect()
    }

    #[test]
    fn imap_mark_read_over_local_server() {
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        // Matching UIDVALIDITY (mock=1, id minted at 1) → proceeds.
        let r = imap_set_flag_in_session(&mut session, "inbox", 2, "\\Seen", true, Some(1));
        session.logout().ok();
        assert!(
            r.is_ok(),
            "mark-read should succeed on a strict server: {:?}",
            r
        );
    }

    #[test]
    fn imap_delete_over_local_server() {
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        let r = imap_delete_message_in_session(&mut session, "inbox", 2, Some(1));
        session.logout().ok();
        assert!(
            r.is_ok(),
            "delete (STORE \\Deleted + UID EXPUNGE) should succeed: {:?}",
            r
        );
    }

    #[test]
    fn imap_move_over_local_server() {
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        let r = imap_move_message_in_session(&mut session, "inbox", 2, "archive", Some(1));
        session.logout().ok();
        assert!(r.is_ok(), "server-side UID MOVE should succeed: {:?}", r);
    }

    #[test]
    fn imap_folder_status_over_local_server() {
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        let status = imap_folder_status_in_session(&mut session, "inbox")
            .expect("STATUS with a parenthesized att list should succeed");
        session.logout().ok();
        // Both counts must round-trip: the bug read them from the (empty)
        // returned Mailbox, so both were 0. three_msgs() are all unread.
        assert_eq!(status.total, 3, "STATUS MESSAGES maps to total");
        assert_eq!(status.unread, 3, "STATUS UNSEEN maps to unread");
    }

    /// The data-safety fix: a mutation must REFUSE when the mailbox's UIDVALIDITY
    /// no longer matches the value the id was minted with (mailbox recreated →
    /// same UID names a different message → delete would nuke the wrong one).
    #[test]
    fn imap_delete_refuses_on_uidvalidity_change() {
        // Server now reports UIDVALIDITY 2; the id was minted at 1.
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            uid_validity: 2,
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        let r = imap_delete_message_in_session(&mut session, "inbox", 2, Some(1));
        session.logout().ok();
        assert!(
            r.is_err(),
            "delete must refuse when UIDVALIDITY changed under the id"
        );
        assert!(
            r.as_ref().unwrap_err().contains("UIDVALIDITY"),
            "error should name the cause, got: {:?}",
            r
        );
    }

    /// The same guard protects mark-read (wrong message silently marked read).
    #[test]
    fn imap_mark_read_refuses_on_uidvalidity_change() {
        let port = start_mock_imap(MockConfig {
            msgs: three_msgs(),
            uid_validity: 99,
            ..Default::default()
        });
        let mut session = connect_and_login(port);
        let r = imap_set_flag_in_session(&mut session, "inbox", 2, "\\Seen", true, Some(1));
        session.logout().ok();
        assert!(r.is_err(), "mark-read must refuse on UIDVALIDITY mismatch");
    }
}

// ---------------------------------------------------------------------------
// Pure-helper coverage: formatters, encoders, decoders, mappers, validators.
// Deterministic: no fs / net / clock / FFI. (Windows-only helpers are gated.)
// ---------------------------------------------------------------------------
#[cfg(test)]
mod pure_helper_tests {
    use super::*;

    // format_iso8601_utc: epoch-seconds → "YYYY-MM-DDTHH:MM:SSZ" via the
    // Hinnant civil-from-days algorithm. Covers epoch, end-of-day, a known
    // landmark, and (critically) NEGATIVE seconds, which exercise the
    // div_euclid/rem_euclid path that plain `/`,`%` would get wrong.
    #[test]
    fn format_iso8601_utc_landmarks_and_negative() {
        assert_eq!(format_iso8601_utc(0), "1970-01-01T00:00:00Z");
        assert_eq!(format_iso8601_utc(86399), "1970-01-01T23:59:59Z");
        // 2021-01-01T00:00:00Z is a well-known epoch landmark.
        assert_eq!(format_iso8601_utc(1609459200), "2021-01-01T00:00:00Z");
        // One second before epoch: the negative-second / borrow case.
        assert_eq!(format_iso8601_utc(-1), "1969-12-31T23:59:59Z");
        // A full day before epoch stays at 00:00:00 (rem_euclid of -86400 is 0).
        assert_eq!(format_iso8601_utc(-86400), "1969-12-31T00:00:00Z");
    }

    // format_iso8601 (u64 variant) must agree with the i64 variant on the
    // non-negative range it shares, since they use the same civil algorithm.
    #[test]
    fn format_iso8601_u64_matches_utc_variant() {
        for &s in &[0u64, 86399, 1609459200, 1700000000] {
            assert_eq!(format_iso8601(s), format_iso8601_utc(s as i64));
        }
    }

    // issue #45a: open_message must NEVER shell-open an empty web_link (IMAP has
    // no web view; "" would ShellExecute → Explorer at the install dir).
    // is_openable_web_link is the guard, and open_message is a logged no-op (Ok)
    // for a non-Outlook id with no link.
    #[test]
    fn issue_45a_empty_web_link_is_not_openable() {
        assert!(!is_openable_web_link(""));
        assert!(!is_openable_web_link("   "));
        assert!(is_openable_web_link("https://mail.example.com/x"));
        // An IMAP id ("imap:" prefix) is not an Outlook id, so with an empty link
        // the call resolves to a no-op success — not an Explorer window.
        assert!(open_message("", "imap:acct|inbox|5|1").is_ok());
    }

    // issue #45b: build_imap_preview strips HTML tags, collapses whitespace runs,
    // and truncates to 200 chars. A message with no Content-Type header at all
    // defaults to text/plain but still gets the tag strip (issue #114): senders
    // that omit the header are the ones who drop raw HTML in the body.
    #[test]
    fn issue_45b_build_imap_preview_snippet() {
        assert_eq!(
            build_imap_preview("", "<p>Hello   <b>world</b>,\r\nthis  is the body.</p>"),
            "Hello world, this is the body.",
        );
        // Irregular whitespace collapses to single spaces.
        assert_eq!(build_imap_preview("", "  a\t\tb\r\n c  "), "a b c");
        // Empty body → empty preview.
        assert_eq!(build_imap_preview("", ""), "");
        // Long body truncates to exactly 200 chars.
        let long = "x ".repeat(300);
        assert_eq!(build_imap_preview("", &long).chars().count(), 200);
    }

    // -----------------------------------------------------------------------
    // issue #114: BODY[TEXT] of a multipart message is the whole MIME body, so
    // the preview showed "This is a multi-part message in MIME format." plus
    // boundary delimiters and part headers instead of the sender's words.
    // -----------------------------------------------------------------------

    /// The exact structure from the report: a multipart/alternative with the
    /// preamble line, a text/plain part and an HTML twin. The plain part wins
    /// and nothing above it leaks in.
    #[test]
    fn issue_114_multipart_preview_is_the_text_part_not_the_scaffolding() {
        let headers = "From: a@b.com\r\nSubject: Hi\r\n\
             Content-Type: multipart/alternative;\r\n \
             boundary=\"------------1D9645AFA03EA13B508AAAAA\"\r\n\r\n";
        let body = "This is a multi-part message in MIME format.\r\n\
             --------------1D9645AFA03EA13B508AAAAA\r\n\
             Content-Type: text/plain; charset=utf-8; format=flowed\r\n\
             Content-Transfer-Encoding: 7bit\r\n\r\n\
             This is the first line of the actual plain text message.\r\n\r\n\
             --------------1D9645AFA03EA13B508AAAAA\r\n\
             Content-Type: text/html; charset=utf-8\r\n\
             Content-Transfer-Encoding: 7bit\r\n\r\n\
             <html><body><p>HTML version</p></body></html>\r\n\
             --------------1D9645AFA03EA13B508AAAAA--\r\n";

        assert_eq!(
            build_imap_preview(headers, body),
            "This is the first line of the actual plain text message.",
        );
    }

    /// The report's second example: a quoted-printable part whose leading line
    /// is nothing but a soft-break artifact (`=20`). Decoding must run before
    /// the whitespace collapse, or the preview reads "=20 Text starts here".
    #[test]
    fn issue_114_quoted_printable_part_is_decoded() {
        let headers =
            "Content-Type: multipart/mixed; boundary=\"----=_Part_3500521_102256291\"\r\n\r\n";
        let body = "------=_Part_3500521_102256291\r\n\
             Content-Type: text/plain; charset=utf-8\r\n\
             Content-Transfer-Encoding: quoted-printable\r\n\r\n\
             \r\n =20\r\nText starts here and wraps a very long w=\r\nord.\r\n\
             ------=_Part_3500521_102256291--\r\n";

        assert_eq!(
            build_imap_preview(headers, body),
            "Text starts here and wraps a very long word.",
        );
    }

    /// No text/plain alternative: the HTML part is used, but its <style> and
    /// <script> blocks are dropped whole — previewing a stylesheet is the same
    /// bug wearing a different hat — and entities are decoded.
    #[test]
    fn issue_114_html_only_preview_drops_style_and_script() {
        let headers = "Content-Type: multipart/related; boundary=bnd\r\n\r\n";
        let body = "--bnd\r\n\
             Content-Type: text/html; charset=utf-8\r\n\r\n\
             <html><head><style>.a { color: #fff; }</style>\
             <script>var x = 1;</script></head>\
             <body><p>Fish&nbsp;&amp;&nbsp;chips</p></body></html>\r\n\
             --bnd--\r\n";

        assert_eq!(build_imap_preview(headers, body), "Fish & chips");
    }

    /// Attachments are not the message's text, even when they are text/*, and
    /// the plain part nested inside a multipart/alternative inside a
    /// multipart/mixed is still found.
    #[test]
    fn issue_114_nested_parts_walked_and_attachments_skipped() {
        let headers = "Content-Type: multipart/mixed; boundary=\"outer\"\r\n\r\n";
        let body = "--outer\r\n\
             Content-Type: multipart/alternative; boundary=\"inner\"\r\n\r\n\
             --inner\r\n\
             Content-Type: text/plain; charset=us-ascii\r\n\r\n\
             Real body text.\r\n\
             --inner--\r\n\
             --outer\r\n\
             Content-Type: text/plain; charset=us-ascii\r\n\
             Content-Disposition: attachment; filename=\"notes.txt\"\r\n\r\n\
             Attachment contents.\r\n\
             --outer--\r\n";

        assert_eq!(build_imap_preview(headers, body), "Real body text.");
    }

    /// A base64 text part decodes; a base64 part that is corrupt is dropped
    /// rather than previewed as base64 soup. A charset the bytes do not match
    /// (cp1252) still yields readable text.
    #[test]
    fn issue_114_transfer_encodings_and_charsets() {
        let b64 = "Content-Type: multipart/mixed; boundary=b\r\n\r\n";
        let body = "--b\r\n\
             Content-Type: text/plain\r\n\
             Content-Transfer-Encoding: base64\r\n\r\n\
             SGVsbG8gZnJvbSBiYXNlNjQu\r\n\
             --b--\r\n";
        assert_eq!(build_imap_preview(b64, body), "Hello from base64.");

        // Corrupt base64 → the part is skipped, so the preview is empty rather
        // than a wall of unreadable characters.
        let broken = "--b\r\n\
             Content-Type: text/plain\r\n\
             Content-Transfer-Encoding: base64\r\n\r\n\
             !!!not base64!!!\r\n\
             --b--\r\n";
        assert_eq!(build_imap_preview(b64, broken), "");

        // Windows-1252 bytes (0x92 = a curly apostrophe) are not valid UTF-8;
        // the charset decides, so the word survives instead of becoming U+FFFD.
        let cp = "Content-Type: multipart/mixed; boundary=b\r\n\r\n";
        let latin = "--b\r\n\
             Content-Type: text/plain; charset=windows-1252\r\n\
             Content-Transfer-Encoding: quoted-printable\r\n\r\n\
             It=92s here.\r\n\
             --b--\r\n";
        assert_eq!(build_imap_preview(cp, latin), "It\u{2019}s here.");
    }

    /// A multipart that declares a boundary it never uses is malformed; the
    /// preview is empty (nothing was found) rather than the delimiter soup the
    /// old implementation surfaced. Same for a part with no readable text.
    #[test]
    fn issue_114_unusable_structures_preview_empty() {
        let headers = "Content-Type: multipart/mixed; boundary=\"never-used\"\r\n\r\n";
        let body = "This is a multi-part message in MIME format.\r\n--other\r\nstuff\r\n";
        assert_eq!(build_imap_preview(headers, body), "");

        // Only a non-text leaf: nothing to preview.
        let img = "Content-Type: multipart/mixed; boundary=b\r\n\r\n";
        let body = "--b\r\n\
             Content-Type: image/png\r\n\
             Content-Transfer-Encoding: base64\r\n\r\n\
             iVBORw0KGgo=\r\n\
             --b--\r\n";
        assert_eq!(build_imap_preview(img, body), "");
    }

    /// A single-part message keeps working: its own Content-Transfer-Encoding
    /// applies, and a declared text/plain is trusted verbatim — a plain-text
    /// `<https://link>` must not be eaten as if it were a tag.
    #[test]
    fn issue_114_single_part_body_decodes_and_keeps_angle_brackets() {
        let headers = "Content-Type: text/plain; charset=utf-8\r\n\
             Content-Transfer-Encoding: quoted-printable\r\n\r\n";
        assert_eq!(
            build_imap_preview(headers, "See <https://example.com/x> for caf=C3=A9.\r\n"),
            "See <https://example.com/x> for caf\u{e9}.",
        );
    }

    /// The header helpers the walk leans on. `parse_header_field_raw` must NOT
    /// run RFC 2047 decoding (a boundary may contain `=?`), while the decoding
    /// `parse_header_field` used for Subject/From is unchanged.
    #[test]
    fn issue_114_content_type_params_parse_without_mime_word_decoding() {
        let hdr = "Content-Type: multipart/mixed; boundary=\"=?_not-a-word?=\"\r\n\
             Subject: =?utf-8?q?caf=C3=A9?=\r\n\r\n";
        assert_eq!(
            mime_param(&parse_header_field_raw(hdr, "content-type"), "boundary").as_deref(),
            Some("=?_not-a-word?="),
        );
        assert_eq!(parse_header_field(hdr, "subject"), "caf\u{e9}");
        // Folded parameter lines still join, and lookup is case-insensitive.
        let folded = "Content-Type: text/plain;\r\n\tcharset=ISO-8859-1\r\n\r\n";
        assert_eq!(
            mime_param(&parse_header_field_raw(folded, "Content-Type"), "CHARSET").as_deref(),
            Some("ISO-8859-1"),
        );
    }

    // percent_encode: RFC 3986 unreserved bytes pass through verbatim; every
    // other byte (incl. multi-byte UTF-8, which is encoded per-byte) becomes
    // %XX uppercase hex. This is the encoding used for ms-outlook:// deep links
    // and mailto subjects, so a stray reserved char must be escaped.
    #[test]
    fn percent_encode_escapes_reserved_keeps_unreserved() {
        assert_eq!(percent_encode("AZaz09-_.~"), "AZaz09-_.~");
        assert_eq!(percent_encode("a b"), "a%20b");
        assert_eq!(percent_encode("Re: hi!"), "Re%3A%20hi%21");
        assert_eq!(percent_encode("a/b?c=d&e"), "a%2Fb%3Fc%3Dd%26e");
        assert_eq!(percent_encode(""), "");
        // Non-ASCII is encoded byte-by-byte as UTF-8 (é = 0xC3 0xA9).
        assert_eq!(percent_encode("é"), "%C3%A9");
    }

    // xml_decode: the five predefined XML entities. Order matters: `&amp;`
    // must be decoded LAST among real inputs, but here it is decoded FIRST,
    // which means a literal "&amp;lt;" double-decodes to "<". Pin that behavior.
    #[test]
    fn xml_decode_entities_and_amp_ordering() {
        assert_eq!(
            xml_decode("a &amp; b &lt;tag&gt; &quot;q&quot; &apos;x&apos;"),
            "a & b <tag> \"q\" 'x'",
        );
        // No entities → unchanged.
        assert_eq!(xml_decode("plain text"), "plain text");
        // amp-first ordering double-decodes this crafted input.
        assert_eq!(xml_decode("&amp;lt;"), "<");
        assert_eq!(xml_decode(""), "");
    }

    // gmail_label_id: logical folder → Gmail label. Unknown folders fall back
    // to INBOX (never panics / never an empty label).
    #[test]
    fn gmail_label_id_mapping() {
        assert_eq!(gmail_label_id("inbox"), "INBOX");
        assert_eq!(gmail_label_id("sent"), "SENT");
        assert_eq!(gmail_label_id("sentitems"), "SENT");
        assert_eq!(gmail_label_id("drafts"), "DRAFT");
        assert_eq!(gmail_label_id("deleteditems"), "TRASH");
        assert_eq!(gmail_label_id("junkemail"), "SPAM");
        assert_eq!(gmail_label_id("archive"), "INBOX"); // unknown → default
        assert_eq!(gmail_label_id(""), "INBOX");
    }

    // is_outlook_id: only native Microsoft/Graph ids may be handed to the
    // ms-outlook:// protocol. Ids prefixed with another provider must NOT be,
    // or the deep link fails silently.
    #[test]
    fn is_outlook_id_excludes_other_providers() {
        assert!(is_outlook_id("AAMkAGI2...")); // bare Graph id
        assert!(is_outlook_id("48d3-guid"));
        assert!(!is_outlook_id("google:117xyz"));
        assert!(!is_outlook_id("imap:bob@host"));
        assert!(!is_outlook_id("caldav:evt"));
        assert!(!is_outlook_id("carddav:c"));
        // Substring-not-prefix must still count as Outlook (only a prefix excludes).
        assert!(is_outlook_id("x-google:not-a-prefix"));
    }

    // url_host: strips scheme + path, returns the authority. Falls back to
    // "unknown" only when there is genuinely nothing before the first slash.
    #[test]
    fn url_host_strips_scheme_and_path() {
        assert_eq!(url_host("https://api.github.com/user"), "api.github.com");
        assert_eq!(url_host("http://example.com"), "example.com");
        assert_eq!(
            url_host("https://host.example.com:8443/dav/cal"),
            "host.example.com:8443"
        );
        assert_eq!(url_host("bare.host/path"), "bare.host");
        // Empty authority (path-only) → empty string, not a panic.
        assert_eq!(url_host("https:///nohost"), "");
    }

    // classify_dav_status [S-46]: an *unauthenticated* PROPFIND, so 401/403 is
    // the healthy answer — the collection is there and wants credentials, which
    // is everything a probe can learn without them. Treating those as failures
    // would badge every working preset as broken.
    #[test]
    fn s46_dav_probe_treats_auth_required_as_reachable() {
        assert_eq!(classify_dav_status(401, false), ProbeOutcome::Reachable);
        assert_eq!(classify_dav_status(403, false), ProbeOutcome::Reachable);
        assert_eq!(classify_dav_status(207, false), ProbeOutcome::Reachable);
        assert_eq!(classify_dav_status(200, false), ProbeOutcome::Reachable);
        assert_eq!(classify_dav_status(301, false), ProbeOutcome::Reachable);
    }

    // A moved/retired DAV path is exactly how a stale preset presents, so 404 /
    // 405 must be distinguishable from "the server is having a bad day" (5xx)
    // and from "nothing answered at all" (Unreachable, set by the transport).
    #[test]
    fn s46_dav_probe_separates_moved_path_from_server_trouble() {
        assert_eq!(classify_dav_status(404, false), ProbeOutcome::NotDav);
        assert_eq!(classify_dav_status(405, false), ProbeOutcome::NotDav);
        assert_eq!(classify_dav_status(410, false), ProbeOutcome::NotDav);
        assert_eq!(classify_dav_status(500, false), ProbeOutcome::ServerError);
        assert_eq!(classify_dav_status(503, false), ProbeOutcome::ServerError);
    }

    // A `DAV:` response header proves the endpoint speaks WebDAV whatever the
    // status line says, so it wins outright.
    #[test]
    fn s46_dav_header_proves_reachable_regardless_of_status() {
        assert_eq!(classify_dav_status(404, true), ProbeOutcome::Reachable);
        assert_eq!(classify_dav_status(500, true), ProbeOutcome::Reachable);
    }

    // The probe target is a tagged union on both sides of the IPC boundary; the
    // frontend sends `{ kind: "imap" | "dav", … }`. A rename on either side must
    // fail here rather than at runtime with a silent deserialize error.
    #[test]
    fn s46_probe_target_matches_the_frontend_tagged_union() {
        let imap: ProbeTarget =
            serde_json::from_str(r#"{"kind":"imap","host":"imap.gmail.com","port":993}"#)
                .expect("imap target");
        match imap {
            ProbeTarget::Imap { host, port } => {
                assert_eq!(host, "imap.gmail.com");
                assert_eq!(port, 993);
            }
            _ => panic!("expected an imap target"),
        }

        let dav: ProbeTarget =
            serde_json::from_str(r#"{"kind":"dav","url":"https://caldav.icloud.com"}"#)
                .expect("dav target");
        assert!(matches!(dav, ProbeTarget::Dav { .. }));

        // An unknown kind is rejected, not silently defaulted.
        assert!(serde_json::from_str::<ProbeTarget>(r#"{"kind":"smtp","host":"x","port":1}"#).is_err());
    }

    // The outcome is a closed set the UI switches on; these are the exact wire
    // strings src/connect/probe.ts expects.
    #[test]
    fn s46_probe_outcome_wire_format_is_snake_case() {
        let ser = |o: ProbeOutcome| serde_json::to_string(&o).unwrap_or_default();
        assert_eq!(ser(ProbeOutcome::Reachable), r#""reachable""#);
        assert_eq!(ser(ProbeOutcome::Unreachable), r#""unreachable""#);
        assert_eq!(ser(ProbeOutcome::NotDav), r#""not_dav""#);
        assert_eq!(ser(ProbeOutcome::ServerError), r#""server_error""#);
    }

    // [S-51] GitHub device flow: the client id is public by design (the grant
    // ships no secret), but an UNSET one must fail loudly with instructions
    // rather than firing a request that returns an opaque GitHub error.
    #[test]
    fn s51_github_sign_in_refuses_until_a_client_id_is_registered() {
        // Mirrors the Google guard. When the id IS set this must stop failing,
        // so the assertion is written against the placeholder, not `is_err()`.
        if GITHUB_CLIENT_ID == "REPLACE_WITH_GITHUB_OAUTH_APP_CLIENT_ID" {
            let err = github_client_id().expect_err("placeholder must not be usable");
            assert!(err.contains("github.com/settings/applications/new"), "names where to register: {err}");
            assert!(err.contains("Device Flow"), "names the required setting: {err}");
            assert!(err.contains("personal access token"), "points at the path that still works: {err}");
        } else {
            assert_eq!(github_client_id().unwrap_or_default(), GITHUB_CLIENT_ID);
        }
    }

    // The scopes must match the token form's, or a widget would work on one
    // GitHub connection and not the other.
    #[test]
    fn s51_github_device_scope_matches_the_token_path() {
        for scope in ["repo", "read:user", "notifications"] {
            assert!(GITHUB_SCOPE.contains(scope), "missing {scope} in {GITHUB_SCOPE}");
        }
    }

    // `authorization_pending` / `slow_down` are the NORMAL states while the user
    // is still on GitHub's approval page — treating either as a failure would
    // abort every sign-in a second after it started. Everything else is real.
    #[test]
    fn s51_github_poll_response_parses_pending_and_terminal_errors() {
        let pending: GitHubDeviceToken =
            serde_json::from_str(r#"{"error":"authorization_pending"}"#).expect("pending");
        assert_eq!(pending.error.as_deref(), Some("authorization_pending"));
        assert!(pending.access_token.is_none());

        let denied: GitHubDeviceToken = serde_json::from_str(
            r#"{"error":"access_denied","error_description":"The user denied the request"}"#,
        )
        .expect("denied");
        assert_eq!(denied.error.as_deref(), Some("access_denied"));

        // Success carries a token and no error; `error_description` is absent,
        // which must not fail deserialization (hence `#[serde(default)]`).
        let ok: GitHubDeviceToken =
            serde_json::from_str(r#"{"access_token":"gho_x","token_type":"bearer","scope":"repo"}"#)
                .expect("success");
        assert_eq!(ok.access_token.as_deref(), Some("gho_x"));
        assert!(ok.error.is_none());
    }

    // The start response drives the UI's code display and poll interval; a
    // field rename on GitHub's side must fail here, not at runtime.
    #[test]
    fn s51_github_device_code_response_parses() {
        let dc: GitHubDeviceCode = serde_json::from_str(
            r#"{"device_code":"dc","user_code":"ABCD-1234",
                "verification_uri":"https://github.com/login/device",
                "expires_in":900,"interval":5}"#,
        )
        .expect("device code");
        assert_eq!(dc.user_code, "ABCD-1234");
        assert_eq!(dc.verification_uri, "https://github.com/login/device");
        assert_eq!(dc.interval, 5);
    }

    // pkce_challenge: deterministic SHA-256 → base64url-nopad of the verifier.
    // Asserts the canonical RFC 7636 Appendix B test vector, so a regression in
    // the hash or the base64 alphabet/padding is caught exactly.
    #[test]
    fn pkce_challenge_matches_rfc7636_vector() {
        assert_eq!(
            pkce_challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"),
            "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
        );
        // URL-safe alphabet + no '=' padding regardless of input length.
        let c = pkce_challenge("x");
        assert!(!c.contains('='), "challenge must be unpadded");
        assert!(
            !c.contains('+') && !c.contains('/'),
            "must use URL-safe alphabet"
        );
    }

    // decode_qp: RFC 2047 "Q" encoding: `_` is space, `=XX` is a hex byte,
    // and an incomplete/invalid escape is passed through literally (no panic).
    #[test]
    fn decode_qp_handles_escapes_and_garbage() {
        assert_eq!(decode_qp("Hello_World"), "Hello World");
        assert_eq!(decode_qp("a=20b"), "a b");
        assert_eq!(decode_qp("100=25"), "100%"); // =25 → '%'
                                                 // Trailing/short escape and invalid hex pass through unchanged.
        assert_eq!(decode_qp("ab="), "ab=");
        assert_eq!(decode_qp("=ZZ"), "=ZZ");
        assert_eq!(decode_qp(""), "");
    }

    // base64_decode_str: standard-alphabet decoder used for RFC 2047 "B" words.
    // Round-trips known vectors; rejects (returns None) on invalid alphabet.
    #[test]
    fn base64_decode_str_known_vectors() {
        assert_eq!(base64_decode_str("aGVsbG8=").as_deref(), Some("hello"));
        assert_eq!(base64_decode_str("aGk=").as_deref(), Some("hi"));
        assert_eq!(base64_decode_str("Zm9vYmE=").as_deref(), Some("fooba"));
        assert_eq!(base64_decode_str("").as_deref(), Some(""));
        // A non-base64 character anywhere → None, never a panic.
        assert_eq!(base64_decode_str("not valid!@#$"), None);
    }

    // decode_mime_words / decode_encoded_word: RFC 2047 header decoding.
    // Covers B and Q words, a plain (un-encoded) tail, whitespace folding
    // between consecutive encoded words, and a malformed word left verbatim.
    #[test]
    fn decode_mime_words_b_q_and_plain() {
        // "Hello" in Base64.
        assert_eq!(decode_mime_words("=?UTF-8?B?SGVsbG8=?="), "Hello");
        // "a b" via Q-encoding (underscore = space).
        assert_eq!(decode_mime_words("=?UTF-8?Q?a_b?="), "a b");
        // Plain text passes through untouched.
        assert_eq!(decode_mime_words("just plain"), "just plain");
        // Encoded word followed by a plain suffix.
        assert_eq!(decode_mime_words("=?UTF-8?B?SGk=?= there"), "Hi there");
        // Whitespace between two adjacent encoded words is collapsed away.
        assert_eq!(
            decode_mime_words("=?UTF-8?B?SGVsbG8=?= =?UTF-8?B?V29ybGQ=?="),
            "HelloWorld",
        );
        // Malformed (no closing ?=) is emitted as-is, never panics.
        assert_eq!(decode_mime_words("=?UTF-8?B?SGk"), "=?UTF-8?B?SGk");
        // Unknown encoding letter → raw text segment returned.
        assert_eq!(decode_encoded_word("=?UTF-8?X?raw?="), "raw");
        // Too-short word returned unchanged.
        assert_eq!(decode_encoded_word("=??="), "=??=");
    }

    // parse_rfc2822_date: best-effort RFC 2822 → ISO 8601. issue #45(d): a numeric
    // zone offset is APPLIED to yield a true UTC instant (`...Z`), not dropped; an
    // unrecognized alpha zone keeps the naive best-effort; an unparseable string is
    // returned verbatim (never panics).
    #[test]
    fn parse_rfc2822_date_formats_and_fallbacks() {
        assert_eq!(
            parse_rfc2822_date("Mon, 01 Apr 2026 12:00:00 +0000"),
            "2026-04-01T12:00:00Z",
        );
        // GMT is also treated as UTC.
        assert_eq!(
            parse_rfc2822_date("Tue, 02 Jun 2025 10:30:05 GMT"),
            "2025-06-02T10:30:05Z",
        );
        // issue #45(d): a positive offset is subtracted to reach UTC (+0200 → -2h).
        assert_eq!(
            parse_rfc2822_date("Wed, 03 Jul 2024 08:09:10 +0200"),
            "2024-07-03T06:09:10Z",
        );
        // issue #45(d): a negative offset (US Pacific) is added to reach UTC.
        assert_eq!(
            parse_rfc2822_date("Thu, 02 Jul 2026 00:51:37 -0700"),
            "2026-07-02T07:51:37Z",
        );
        // issue #45(d): the offset rolls the instant across a day/month/year edge.
        assert_eq!(
            parse_rfc2822_date("Wed, 01 Jan 2025 01:00:00 +0200"),
            "2024-12-31T23:00:00Z",
        );
        // A colon-form offset is accepted too.
        assert_eq!(
            parse_rfc2822_date("Wed, 03 Jul 2024 08:09:10 +02:00"),
            "2024-07-03T06:09:10Z",
        );
        // An unrecognized alpha zone → naive best-effort (no invented offset).
        assert_eq!(
            parse_rfc2822_date("Wed, 03 Jul 2024 08:09:10 EST"),
            "2024-07-03T08:09:10",
        );
        // Missing the comma-separated weekday still parses.
        assert_eq!(
            parse_rfc2822_date("15 Dec 1999 23:59:59 +0000"),
            "1999-12-15T23:59:59Z",
        );
        // Too few fields → echoed back unchanged.
        assert_eq!(parse_rfc2822_date("garbage"), "garbage");
    }

    // unfold_ical / get_ical_prop / get_ical_line: the RFC 5545/6350 line model.
    // Continuation lines (leading space/tab) join to the previous line; property
    // lookup is case-insensitive and parameter-aware.
    #[test]
    fn ical_unfold_and_property_lookup() {
        let folded = "SUMMARY:Hello \r\n World\r\nDTSTART;TZID=UTC:20260401T090000Z\r\n";
        let unfolded = unfold_ical(folded);
        assert_eq!(
            unfolded,
            "SUMMARY:Hello World\nDTSTART;TZID=UTC:20260401T090000Z"
        );
        // get_ical_prop strips parameters and is case-insensitive on the name.
        assert_eq!(get_ical_prop(&unfolded, "summary"), "Hello World");
        assert_eq!(get_ical_prop(&unfolded, "DTSTART"), "20260401T090000Z");
        // Absent property → empty string.
        assert_eq!(get_ical_prop(&unfolded, "LOCATION"), "");
        // get_ical_line preserves the whole line including parameters.
        assert_eq!(
            get_ical_line(&unfolded, "dtstart"),
            "DTSTART;TZID=UTC:20260401T090000Z"
        );
        assert_eq!(get_ical_line(&unfolded, "missing"), "");
    }

    // is_microsoft_web_link (Windows-only): decides whether a calendar event
    // hands off to the ms-outlook:// protocol. Must match Microsoft surfaces and
    // reject third-party ones (e.g. Google), or the deep link is wrong.
    #[cfg(target_os = "windows")]
    #[test]
    fn is_microsoft_web_link_matches_ms_surfaces() {
        assert!(is_microsoft_web_link(
            "https://outlook.office.com/calendar/item/1"
        ));
        assert!(is_microsoft_web_link("https://outlook.live.com/owa/"));
        assert!(is_microsoft_web_link("https://outlook.office365.com/x"));
        assert!(is_microsoft_web_link("https://www.office.com/launch"));
        assert!(!is_microsoft_web_link("https://calendar.google.com/event"));
        assert!(!is_microsoft_web_link(""));
    }
}

#[cfg(test)]
mod ssrf_fetch_tests {
    use super::*;

    // SSRF (c) regression: stored URLs/hosts are re-validated at FETCH time, not
    // only when the account was added, closing the DNS-rebinding window where a
    // host that resolved public at add-time later resolves to a private address.
    // The guard runs before any network I/O, so a loopback target fails fast.

    #[tokio::test]
    async fn caldav_fetch_revalidates_and_blocks_private_url() {
        widget_netguard::set_bypass(false);
        let r = fetch_caldav_events(
            "http://127.0.0.1/dav/",
            "user",
            "pass",
            "2020-01-01T00:00:00Z",
            "2020-01-02T00:00:00Z",
        )
        .await;
        let Err(e) = r else {
            panic!("loopback CalDAV URL must be blocked at fetch time")
        };
        assert!(e.contains("private/reserved"), "got: {e}");
    }

    #[tokio::test]
    async fn carddav_fetch_revalidates_and_blocks_private_url() {
        widget_netguard::set_bypass(false);
        let r = fetch_carddav_contacts("http://127.0.0.1/dav/", "user", "pass", "", 10).await;
        let Err(e) = r else {
            panic!("loopback CardDAV URL must be blocked at fetch time")
        };
        assert!(e.contains("private/reserved"), "got: {e}");
    }

    #[test]
    fn imap_refetch_revalidates_and_blocks_private_host() {
        widget_netguard::set_bypass(false);
        let r = fetch_imap_messages_sync(
            "127.0.0.1".to_string(),
            993,
            "user".to_string(),
            "pass".to_string(),
            "INBOX".to_string(),
            10,
            "acct".to_string(),
            "e@example.com".to_string(),
            String::new(),
            "imap".to_string(),
        );
        let Err(e) = r else {
            panic!("loopback IMAP host must be blocked at refetch")
        };
        assert!(e.contains("private/reserved"), "got: {e}");
    }
}
