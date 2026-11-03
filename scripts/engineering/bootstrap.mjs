// Local development only. Generated credentials are never committed or sent to a browser bundle.
import { randomBytes } from "node:crypto";
import { mkdir, readFile, writeFile, chmod } from "node:fs/promises";
import { resolve } from "node:path";
import { spawn } from "node:child_process";
import { command } from "./lab.mjs";
const directory = resolve(".tools/engineering");
await readFile(resolve("NexusHR.slnx"));
await mkdir(directory, { recursive: true });
if (process.platform === "win32") {
  const identity = `${process.env.USERDOMAIN}\\${process.env.USERNAME}`;
  await new Promise((done, reject) => {
    const child = spawn(
      "icacls",
      [
        directory,
        "/inheritance:r",
        "/grant:r",
        `${identity}:(OI)(CI)F`,
        "SYSTEM:(OI)(CI)F",
      ],
      { windowsHide: true, stdio: "ignore" },
    );
    child.on("error", reject);
    child.on("exit", (code) =>
      code === 0
        ? done()
        : reject(new Error("Cannot protect generated credential directory")),
    );
  });
}
const secret = () => randomBytes(32).toString("hex");
let values;
try {
  values = JSON.parse(
    await readFile(resolve(directory, "access.json"), "utf8"),
  );
} catch (e) {
  if (e.code !== "ENOENT") throw e;
  values = {
    database: secret(),
    agent: secret(),
    grafana: secret(),
    monitor: secret(),
    broker: secret(),
    lab: secret(),
    operator: secret(),
    reader: secret(),
  };
}
for (const key of ["labDb", "labBroker", "labStorage"])
  values[key] ??= secret();
await writeFile(resolve(directory, "access.json"), JSON.stringify(values), {
  mode: 0o600,
});
await chmod(resolve(directory, "access.json"), 0o600);
const envPath = resolve(directory, "engineering.env");
const realmPath = resolve(directory, "lab-realm.json");
const env = {
  ENGINEERING_ENV_FILE: envPath.replaceAll("\\", "/"),
  ENGINEERING_LAB_REALM_FILE: realmPath.replaceAll("\\", "/"),
  ENGINEERING_DB_PASSWORD: values.database,
  ENGINEERING_AGENT_KEY: values.agent,
  ENGINEERING_GRAFANA_PASSWORD: values.grafana,
  ENGINEERING_LAB_DB_PASSWORD: values.labDb,
  ENGINEERING_LAB_BROKER_PASSWORD: values.labBroker,
  ENGINEERING_LAB_STORAGE_PASSWORD: values.labStorage,
  Engineering__Enabled: "true",
  Engineering__ExecutionEnabled: "true",
  Engineering__InitializeDatabase: "true",
  Engineering__AgentKey: values.agent,
  Engineering__Database: `Host=engineering-db;Database=nexushr_engineering;Username=engineering;Password=${values.database};Maximum Pool Size=10;Command Timeout=10`,
  Engineering__BrokerUser: "engineering-monitor",
  Engineering__BrokerPassword: values.broker,
};
for (const service of ["candidate", "hiring", "onboarding"])
  env[`Engineering__Databases__${service}`] =
    `Host=${service}-db;Database=nexushr_${service};Username=engineering_monitor;Password=${values.monitor}`;
await writeFile(
  envPath,
  Object.entries(env)
    .map(([k, v]) => `${k}=${v}`)
    .join("\n") + "\n",
  { mode: 0o600 },
);
const realm = {
  realm: "nexushr-engineering-lab",
  enabled: true,
  roles: { realm: [{ name: "HrSpecialist" }] },
  clients: [
    {
      clientId: "nexushr-lab",
      protocol: "openid-connect",
      enabled: true,
      publicClient: false,
      secret: values.lab,
      serviceAccountsEnabled: true,
      standardFlowEnabled: false,
      directAccessGrantsEnabled: false,
      fullScopeAllowed: true,
      protocolMappers: [
        {
          name: "realm roles",
          protocol: "openid-connect",
          protocolMapper: "oidc-usermodel-realm-role-mapper",
          config: {
            multivalued: "true",
            "claim.name": "realm_access.roles",
            "jsonType.label": "String",
            "access.token.claim": "true",
          },
        },
      ],
    },
  ],
  users: [
    {
      username: "service-account-nexushr-lab",
      enabled: true,
      serviceAccountClientId: "nexushr-lab",
      realmRoles: ["HrSpecialist"],
    },
  ],
};
await writeFile(realmPath, JSON.stringify(realm), { mode: 0o600 });
async function request(url, init) {
  const r = await fetch(url, { ...init, signal: AbortSignal.timeout(15000) });
  if (!r.ok && r.status !== 409)
    throw new Error(`Provisioning rejected (${r.status}); details withheld`);
  return r;
}
if (process.argv.includes("--provision")) {
  if (process.argv.includes("--from-development-containers")) {
    const identity = JSON.parse(
      await command(["inspect", "nexushr-keycloak"]),
    )[0];
    const broker = JSON.parse(
      await command(["inspect", "nexushr-rabbitmq"]),
    )[0];
    if (
      identity.Config.Labels["com.docker.compose.project"] !== "nexushr" ||
      !identity.Config.Cmd.includes("start-dev") ||
      broker.Config.Labels["com.docker.compose.project"] !== "nexushr"
    )
      throw new Error("Development container verification failed");
    const value = (container, name) =>
      container.Config.Env.find((v) => v.startsWith(name + "="))?.slice(
        name.length + 1,
      );
    process.env.ENGINEERING_KEYCLOAK_ADMIN_PASSWORD = value(
      identity,
      "KC_BOOTSTRAP_ADMIN_PASSWORD",
    );
    process.env.ENGINEERING_KEYCLOAK_ADMIN_USER = value(
      identity,
      "KC_BOOTSTRAP_ADMIN_USERNAME",
    );
    process.env.ENGINEERING_RABBIT_ADMIN_PASSWORD = value(
      broker,
      "RABBITMQ_DEFAULT_PASS",
    );
    process.env.ENGINEERING_RABBIT_ADMIN_USER = value(
      broker,
      "RABBITMQ_DEFAULT_USER",
    );
  }
  const password = process.env.ENGINEERING_KEYCLOAK_ADMIN_PASSWORD;
  const brokerPassword = process.env.ENGINEERING_RABBIT_ADMIN_PASSWORD;
  if (!password || !brokerPassword)
    throw new Error(
      "Supply development Keycloak/RabbitMQ administrator passwords through environment variables.",
    );
  const token = await (
    await request(
      "http://localhost:8080/realms/master/protocol/openid-connect/token",
      {
        method: "POST",
        body: new URLSearchParams({
          grant_type: "password",
          client_id: "admin-cli",
          username: process.env.ENGINEERING_KEYCLOAK_ADMIN_USER ?? "admin",
          password,
        }),
      },
    )
  ).json();
  const root = "http://localhost:8080/admin/realms/nexushr";
  const headers = {
    Authorization: `Bearer ${token.access_token}`,
    "Content-Type": "application/json",
  };
  for (const role of ["EngineeringReader", "EngineeringOperator"])
    await request(`${root}/roles`, {
      method: "POST",
      headers,
      body: JSON.stringify({ name: role }),
    });
  for (const [name, role] of [
    ["reader", "EngineeringReader"],
    ["operator", "EngineeringOperator"],
  ]) {
    const username = `engineering.${name}`;
    const response = await request(`${root}/users`, {
      method: "POST",
      headers,
      body: JSON.stringify({
        username,
        enabled: true,
        emailVerified: true,
        firstName: "Engineering",
        lastName: name,
        email: `engineering-${name}@example.test`,
        credentials: [
          { type: "password", value: values[name], temporary: false },
        ],
      }),
    });
    const users = await (
      await request(`${root}/users?username=${username}&exact=true`, {
        headers,
      })
    ).json();
    if (users.length !== 1) throw new Error("Ambiguous engineering identity");
    if (
      response.status === 409 &&
      !process.argv.includes("--existing-owned-users")
    )
      throw new Error(
        "Technical user exists; refusing to reset a password. For an idempotent repeat with the original access.json, use --existing-owned-users.",
      );
    const mapping = await (
      await request(`${root}/roles/${role}`, { headers })
    ).json();
    await request(`${root}/users/${users[0].id}`, {
      method: "PUT",
      headers,
      body: JSON.stringify({
        firstName: "Engineering",
        lastName: name,
        email: `engineering-${name}@example.test`,
        emailVerified: true,
      }),
    });
    await request(`${root}/users/${users[0].id}/role-mappings/realm`, {
      method: "POST",
      headers,
      body: JSON.stringify([mapping]),
    });
  }
  const basic = {
    Authorization: `Basic ${Buffer.from(`${process.env.ENGINEERING_RABBIT_ADMIN_USER ?? "nexushr"}:${brokerPassword}`).toString("base64")}`,
    "Content-Type": "application/json",
  };
  await request("http://localhost:15672/api/users/engineering-monitor", {
    method: "PUT",
    headers: basic,
    body: JSON.stringify({ password: values.broker, tags: "monitoring" }),
  });
  await request(
    "http://localhost:15672/api/permissions/%2F/engineering-monitor",
    {
      method: "PUT",
      headers: basic,
      body: JSON.stringify({ configure: "^$", write: "^$", read: "^$" }),
    },
  );
  for (const service of ["candidate", "hiring", "onboarding"]) {
    const sql = `DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname='engineering_monitor') THEN CREATE ROLE engineering_monitor LOGIN PASSWORD '${values.monitor}'; END IF; END $$; GRANT pg_monitor TO engineering_monitor;
      CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
      CREATE OR REPLACE VIEW engineering_message_counts AS SELECT (SELECT count(*) FROM "OutboxMessage") AS outbox_rows,(SELECT count(*) FROM "OutboxState") AS outbox_state_rows,(SELECT count(*) FROM "InboxState") AS inbox_rows;
      GRANT SELECT ON engineering_message_counts TO engineering_monitor;`;
    await new Promise((done, reject) => {
      const child = spawn(
        process.env.DOCKER_EXE ?? "docker",
        [
          "exec",
          "-i",
          `nexushr-${service}-db`,
          "psql",
          "-q",
          "-v",
          "ON_ERROR_STOP=1",
          "-U",
          "postgres",
          "-d",
          `nexushr_${service}`,
        ],
        { stdio: ["pipe", "ignore", "ignore"], windowsHide: true },
      );
      child.stdin.end(sql);
      child.on("error", reject);
      child.on("exit", (code) =>
        code === 0
          ? done()
          : reject(
              new Error(
                "Monitor-role provisioning failed; SQL/credentials withheld",
              ),
            ),
      );
    });
  }
  console.log(
    "Explicit technical roles/users and metadata-only broker/PG accounts provisioned. Existing business users/data were not reset.",
  );
}
console.log(
  "Private development files generated in .tools/engineering. On Windows, restrict their ACL to your account. No credentials printed.",
);
