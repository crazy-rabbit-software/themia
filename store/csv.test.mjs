// node --test store/csv.test.mjs
import assert from "node:assert/strict";
import { test } from "node:test";
import { parse, write } from "./csv.mjs";

test("quoted cells keep their commas, quotes and line breaks", () => {
  const rows = parse('Field,ID,en-us\r\nDescription,2,"one, ""two""\nthree"\r\nTitle,1,Themia\r\n');
  assert.deepEqual(rows, [
    ["Field", "ID", "en-us"],
    ["Description", "2", 'one, "two"\nthree'],
    ["Title", "1", "Themia"],
  ]);
});

test("a last line without a line break is still a row", () => {
  assert.deepEqual(parse("a,b\r\nc,"), [["a", "b"], ["c", ""]]);
});

test("written and read again, the rows are the same", () => {
  const rows = [["Field", "de-de"], ["Feature1", 'Dein Tag, "direkt"\nin der Taskleiste'], ["Empty", ""]];
  assert.deepEqual(parse(write(rows).replace(/^﻿/, "")), rows);
});
