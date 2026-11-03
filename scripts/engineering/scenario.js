import http from "k6/http";
import { sleep } from "k6";
import exec from "k6/execution";
const vus = +__ENV.LAB_VUS,
  duration = +__ENV.LAB_DURATION,
  ramp = +__ENV.LAB_RAMP,
  budget = +__ENV.LAB_REQUEST_BUDGET;
if (
  !Number.isInteger(vus) ||
  vus < 1 ||
  vus > 10000 ||
  duration < 5 ||
  duration > 120 ||
  ramp < 0 ||
  ramp > 20
)
  throw new Error("Invalid lab budget");
const constant = {
  executor: "ramping-vus",
  startVUs: ramp ? 0 : vus,
  stages: [
    ...(ramp ? [{ duration: `${ramp}s`, target: vus }] : []),
    { duration: `${duration}s`, target: vus },
    ...(ramp ? [{ duration: `${ramp}s`, target: 0 }] : []),
  ],
  gracefulRampDown: "2s",
  gracefulStop: "2s",
};
const rate = +__ENV.LAB_RATE;
const arrival = {
  executor: "ramping-arrival-rate",
  startRate: ramp ? 0 : rate,
  timeUnit: "1s",
  stages: [
    ...(ramp ? [{ duration: `${ramp}s`, target: rate }] : []),
    { duration: `${duration}s`, target: rate },
    ...(ramp ? [{ duration: `${ramp}s`, target: 0 }] : []),
  ],
  preAllocatedVUs: vus,
  maxVUs: vus,
  gracefulStop: "2s",
};
export const options = {
  scenarios: { lab: __ENV.LAB_SCENARIO === "arrival" ? arrival : constant },
  discardResponseBodies: true,
  maxRedirects: 0,
  summaryTrendStats: ["avg", "p(50)", "p(95)", "p(99)"],
};
export default function () {
  if (exec.vu.iterationInScenario >= Math.floor(budget / vus)) {
    sleep(1);
    return;
  }
  const root = "http://gateway:8080"; // No environment URL or client-provided target can change this.
  const path =
    __ENV.LAB_TARGET === "overview"
      ? "/api/candidate-overview?page=1&pageSize=20"
      : __ENV.LAB_TARGET === "health"
        ? "/health/ready"
        : "/api/candidates";
  const headers = {
    Authorization: `Bearer ${__ENV.LAB_TOKEN}`,
    "Content-Type": "application/json",
  };
  if (__ENV.LAB_TARGET === "candidates")
    http.post(
      root + path,
      JSON.stringify({
        firstName: "Synthetic",
        lastName: "Lab",
        email: `engineering-${__ENV.LAB_RUN}-${exec.vu.idInTest}-${exec.vu.iterationInScenario}@example.test`,
        phoneNumber: "05555555555",
      }),
      { headers, timeout: "5s", tags: { scenario: "synthetic-create" } },
    );
  else
    http.get(root + path, {
      headers,
      timeout: "5s",
      tags: { scenario: "synthetic-read" },
    });
  sleep(0.05);
}
