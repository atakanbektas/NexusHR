import { test, expect } from "@playwright/test";

test("legacy query offer links still submit their token through the existing API body", async ({ page }) => {
  const token = "synthetic-legacy-query-capability";
  let submitted = false;
  await page.route("**/api/public/offers/respond", async route => {
    expect(route.request().postDataJSON()).toMatchObject({ token, decision: "Accept" });
    submitted = true; await route.fulfill({ status: 204 });
  });
  await page.goto(`/public/offers/respond?token=${token}`);
  page.on("dialog", dialog => dialog.accept());
  await page.getByRole("button", { name: "Teklifi kabul et", exact: true }).click();
  await expect(page.getByText(/İş teklifini kabul ettiğiniz/)).toBeVisible(); expect(submitted).toBe(true);
});
