import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 120_000,
  expect: { timeout: 15_000 },
  workers: 1,
  retries: 0,
  use: { baseURL: process.env.E2E_WEB_URL ?? "http://localhost:5173", trace: "retain-on-failure",
    actionTimeout: 15_000, navigationTimeout: 30_000 },
  reporter: [["list"], ["html", { open: "never" }]],
});
