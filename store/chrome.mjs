// Headless Chrome (or Edge) as the Store pictures' renderer: an HTML page to a PNG at a given size.
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

export const repo = resolve(dirname(fileURLToPath(import.meta.url)), "..");
/** Everything made here: captures, wallpapers, pages. Git-ignored. */
export const out = join(repo, "store", "out");
const work = join(out, "compose");

function browser() {
  const candidates = [
    join(process.env["ProgramFiles"] ?? "", "Google", "Chrome", "Application", "chrome.exe"),
    join(process.env["ProgramFiles(x86)"] ?? "", "Microsoft", "Edge", "Application", "msedge.exe"),
    join(process.env["ProgramFiles"] ?? "", "Microsoft", "Edge", "Application", "msedge.exe"),
  ];
  const found = candidates.find((c) => existsSync(c));
  if (!found) throw new Error("store: neither Chrome nor Edge is installed");
  return found;
}

let chrome;

/**
 * Renders html at width x height CSS px, scale times its pixels, to png. With cut, the picture is the top-left
 * cut.w x cut.h pixels of that: Chrome's window has a least size, and leaves the last rows of a window undrawn under
 * turned elements, so a page is drawn in a taller window and cut back.
 */
export function render(name, html, png, width, height, scale, cut) {
  chrome ??= browser();
  mkdirSync(work, { recursive: true });
  mkdirSync(dirname(png), { recursive: true });
  const file = join(work, name + ".html");
  writeFileSync(file, html);
  execFileSync(chrome, [
    "--headless=new",
    "--disable-gpu",
    "--hide-scrollbars",
    "--default-background-color=00000000",
    `--force-device-scale-factor=${scale}`,
    `--window-size=${width},${height}`,
    `--screenshot=${png}`,
    "--virtual-time-budget=4000",
    pathToFileURL(file).href,
  ], { stdio: "ignore" });
  if (!existsSync(png)) throw new Error(`store: ${png} was not made`);
  if (cut) execFileSync("powershell", ["-NoProfile", "-Command", crop(png, cut.w, cut.h)], { stdio: "ignore" });
  console.log(`  ${png}`);
}

/** A PowerShell line that cuts png to its top-left w x h, keeping its transparency. */
function crop(png, w, h) {
  const p = png.replace(/'/g, "''");
  return `Add-Type -AssemblyName System.Drawing; $b = [Drawing.Bitmap]::FromFile('${p}'); $c = $b.Clone((New-Object Drawing.Rectangle 0, 0, ${w}, ${h}), $b.PixelFormat); $b.Dispose(); $c.Save('${p}', [Drawing.Imaging.ImageFormat]::Png); $c.Dispose()`;
}

/** A PNG saved again as a JPEG of that quality (1 to 100) beside or elsewhere; the PNG is left as it is. */
export function toJpeg(png, jpg, quality) {
  mkdirSync(dirname(jpg), { recursive: true });
  const [from, to] = [png, jpg].map((p) => p.replace(/'/g, "''"));
  execFileSync("powershell", ["-NoProfile", "-Command",
    `Add-Type -AssemblyName System.Drawing; $b = [Drawing.Bitmap]::FromFile('${from}'); $c = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq 'image/jpeg' }; $p = New-Object Drawing.Imaging.EncoderParameters 1; $p.Param[0] = New-Object Drawing.Imaging.EncoderParameter ([Drawing.Imaging.Encoder]::Quality), ([long]${quality}); $b.Save('${to}', $c, $p); $b.Dispose()`], { stdio: "ignore" });
  if (!existsSync(jpg)) throw new Error(`store: ${jpg} was not made`);
  console.log(`  ${jpg}`);
}

export const url = (path) => pathToFileURL(path).href;
