import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { resolve } from "node:path";
export const project = "nexushr-engineering-lab";
export const directory = resolve(".tools/engineering");
export const docker = process.env.DOCKER_EXE ?? "docker";
export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
export async function command(
  args,
  timeout = 30000,
  environment = process.env,
) {
  return await new Promise((done, reject) => {
    const child = spawn(docker, args, {
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
      env: environment,
    });
    let out = "";
    let length = 0;
    child.stdout.on("data", (data) => {
      length += data.length;
      if (length > 4_000_000) child.kill();
      else out += data;
    });
    child.stderr.resume();
    const timer = setTimeout(() => child.kill(), timeout);
    child.on("error", () => {
      clearTimeout(timer);
      reject(new Error("DockerOperationFailed"));
    });
    child.on("exit", (code) => {
      clearTimeout(timer);
      code === 0 ? done(out) : reject(new Error("DockerOperationFailed"));
    });
  });
}
export const compose = (...args) =>
  command(
    [
      "compose",
      "--env-file",
      resolve(directory, "engineering.env"),
      "-p",
      project,
      "-f",
      "docker-compose.engineering-lab.yml",
      ...args,
    ],
    300000,
  );
export async function containers() {
  const ids = (
    await command([
      "ps",
      "-aq",
      "--filter",
      `label=com.docker.compose.project=${project}`,
    ])
  )
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  if (ids.length < 8 || ids.some((id) => !/^[a-f0-9]{12,64}$/.test(id)))
    throw new Error("InvalidLab");
  const data = JSON.parse(await command(["inspect", ...ids]));
  validateContainers(data);
  return data;
}
export function validateContainers(data) {
  const required = [
    "candidate-db",
    "hiring-db",
    "rabbitmq",
    "candidate-storage",
    "lab-keycloak",
    "fault-proxy",
    "candidate-api",
    "hiring-api",
    "gateway",
    "lab-ollama",
  ];
  if (
    required.some(
      (s) =>
        !data.some((c) => c.Config.Labels["com.docker.compose.service"] === s),
    )
  )
    throw new Error("InvalidLab");
  for (const c of data) {
    if (
      c.Config.Labels["com.docker.compose.project"] !== project ||
      c.Config.Labels["nexushr.engineering.disposable"] !== "true"
    )
      throw new Error("InvalidLab");
    const service = c.Config.Labels["com.docker.compose.service"];
    // Business data mounts and Docker sockets are forbidden in every target.
    if (
      (c.Mounts ?? []).some(
        (m) =>
          (m.Type === "volume" &&
            !(
              service === "lab-ollama" &&
              m.Name === "nexushr_ollama-models" &&
              m.Destination === "/root/.ollama/models" &&
              !m.RW &&
              c.HostConfig.Mounts?.some(
                (mount) =>
                  mount.Target === "/root/.ollama/models" &&
                  (mount.VolumeOptions?.Subpath ??
                    mount.VolumeOptions?.SubPath) === "models",
              )
            )) ||
          (m.Type === "bind" &&
            !(
              service === "lab-keycloak" &&
              m.Destination ===
                "/opt/keycloak/data/import/nexushr-engineering-lab-realm.json" &&
              !m.RW
            )),
      )
    )
      throw new Error("InvalidLab");
  }
}
export async function configureModel(model) {
  if (!["qwen3:4b", "qwen2.5vl:3b"].includes(model))
    throw new Error("InvalidModel");
  await containers();
  await command(
    [
      "compose",
      "--env-file",
      resolve(directory, "engineering.env"),
      "-p",
      project,
      "-f",
      "docker-compose.engineering-lab.yml",
      "up",
      "-d",
      "--no-deps",
      "candidate-api",
    ],
    120000,
    { ...process.env, ENGINEERING_LAB_MODEL: model },
  );
}
export async function proxy(path, method = "GET", body) {
  const r = await fetch(`http://127.0.0.1:5474${path}`, {
    method,
    body: body === undefined ? undefined : JSON.stringify(body),
    headers: { "Content-Type": "application/json" },
    signal: AbortSignal.timeout(5000),
  });
  if (!r.ok) throw new Error("ProxyOperationFailed");
  return r.status === 204 ? null : await r.json();
}
export async function initProxies() {
  const existing = await proxy("/proxies");
  for (const [name, listen, upstream] of [
    ["database", "0.0.0.0:8666", "candidate-db:5432"],
    ["broker", "0.0.0.0:8667", "rabbitmq:5672"],
  ])
    if (!existing[name])
      await proxy("/proxies", "POST", {
        name,
        listen,
        upstream,
        enabled: true,
      });
}
export async function recoverLab() {
  await containers();
  await initProxies();
  for (const name of ["database", "broker"]) {
    const state = await proxy(`/proxies/${name}`);
    for (const toxic of state.toxics ?? [])
      await proxy(
        `/proxies/${name}/toxics/${encodeURIComponent(toxic.name)}`,
        "DELETE",
      );
    await proxy(`/proxies/${name}`, "POST", { enabled: true });
  }
  await compose("start", "candidate-api");
}
let cachedToken = null,
  tokenExpires = 0;
export async function token() {
  if (cachedToken && tokenExpires > Date.now()) return cachedToken;
  const access = JSON.parse(
    await readFile(resolve(directory, "access.json"), "utf8"),
  );
  const r = await fetch(
    "http://127.0.0.1:5455/realms/nexushr-engineering-lab/protocol/openid-connect/token",
    {
      method: "POST",
      body: new URLSearchParams({
        grant_type: "client_credentials",
        client_id: "nexushr-lab",
        client_secret: access.lab,
      }),
      signal: AbortSignal.timeout(10000),
    },
  );
  if (!r.ok) throw new Error("LabAuthenticationFailed");
  const value = await r.json();
  cachedToken = value.access_token;
  tokenExpires = Date.now() + Math.max(1, value.expires_in - 30) * 1000;
  return cachedToken;
}
export async function labRequest(path, init = {}) {
  if (
    typeof path !== "string" ||
    path.includes("..") ||
    path.includes("\\") ||
    path.includes("://") ||
    (path !== "" && !/^[/?]/.test(path))
  )
    throw new Error("InvalidLabPath");
  const response = await fetch("http://127.0.0.1:5450/api/candidates" + path, {
    ...init,
    headers: { Authorization: `Bearer ${await token()}`, ...init.headers },
    redirect: "error",
    signal: AbortSignal.timeout(10000),
  });
  if (!response.ok) {
    const error = new Error("LabRequestFailed");
    error.status = response.status;
    throw error;
  }
  return response.status === 204 ? null : await response.json();
}
export async function probe() {
  const start = performance.now();
  try {
    const r = await fetch(
      "http://127.0.0.1:5450/api/candidate-overview?page=1&pageSize=1",
      {
        headers: { Authorization: `Bearer ${await token()}` },
        signal: AbortSignal.timeout(5000),
      },
    );
    return {
      healthy: r.ok,
      timeout: false,
      latencyMs: performance.now() - start,
    };
  } catch (e) {
    return {
      healthy: false,
      timeout: e.name === "TimeoutError",
      latencyMs: performance.now() - start,
    };
  }
}
export async function available() {
  return (await probe()).healthy;
}
if (process.argv.includes("--initialize")) {
  await initProxies();
  console.log("Only synthetic lab database/broker proxies initialized.");
}
