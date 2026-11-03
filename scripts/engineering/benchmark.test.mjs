import { test } from "node:test";
import assert from "node:assert/strict";
import { settleAnalysis } from "./benchmark.mjs";

test("terminal analyses are preserved without another cancellation", async () => {
  const calls = [];
  await settleAnalysis(
    "/synthetic/cv-analyses",
    "fixed",
    async (path, init) => {
      calls.push({ path, init });
      return { status: "Succeeded", version: 7 };
    },
  );
  assert.equal(calls.length, 1);
});
test("benchmark cleanup uses the latest version and waits for durable cancellation", async () => {
  const calls = [];
  let reads = 0;
  await settleAnalysis(
    "/synthetic/cv-analyses",
    "fixed",
    async (path, init) => {
      calls.push({ path, init });
      if (init) return {};
      return { status: ++reads < 3 ? "Running" : "Cancelled", version: 12 };
    },
    async () => {},
  );
  assert.equal(calls[1].init.headers["If-Match"], '"12"');
  assert.equal(reads, 3);
});
test("an unresolved model job fails cleanup rather than releasing a successful slot", async () => {
  let reads = 0;
  await assert.rejects(
    settleAnalysis(
      "/synthetic/cv-analyses",
      "fixed",
      async () => {
        reads++;
        return { status: "Running", version: 2 };
      },
      async () => {},
    ),
    /ModelJobNotSettled/,
  );
  assert.equal(reads, 32);
});
