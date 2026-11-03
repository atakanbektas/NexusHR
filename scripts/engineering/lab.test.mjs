import { test } from "node:test";
import assert from "node:assert/strict";
import { validateContainers, project } from "./lab.mjs";
const services = [
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
const fixture = () =>
  services.map((service) => ({
    Config: {
      Labels: {
        "com.docker.compose.project": project,
        "com.docker.compose.service": service,
        "nexushr.engineering.disposable": "true",
      },
    },
    HostConfig: { Mounts: [] },
    Mounts: [],
  }));
test("execution requires all allowlisted lab services", () => {
  const data = fixture();
  assert.doesNotThrow(() => validateContainers(data));
  assert.throws(() => validateContainers(data.slice(1)), /InvalidLab/);
});
test("business project labels and writable user volumes are rejected", () => {
  const wrong = fixture();
  wrong[0].Config.Labels["com.docker.compose.project"] = "nexushr";
  assert.throws(() => validateContainers(wrong), /InvalidLab/);
  const volume = fixture();
  volume[0].Mounts = [
    { Type: "volume", Name: "nexushr_candidate-db-data", RW: true },
  ];
  assert.throws(() => validateContainers(volume), /InvalidLab/);
});
test("only a read-only model subpath is accepted, not the Ollama root/history", () => {
  const data = fixture(),
    c = data.at(-1);
  c.Mounts = [
    {
      Type: "volume",
      Name: "nexushr_ollama-models",
      Destination: "/root/.ollama/models",
      RW: false,
    },
  ];
  c.HostConfig.Mounts = [
    { Target: "/root/.ollama/models", VolumeOptions: { Subpath: "models" } },
  ];
  assert.doesNotThrow(() => validateContainers(data));
  c.Mounts[0].RW = true;
  assert.throws(() => validateContainers(data), /InvalidLab/);
  c.Mounts[0].RW = false;
  c.HostConfig.Mounts[0].VolumeOptions.Subpath = "";
  assert.throws(() => validateContainers(data), /InvalidLab/);
});
test("Docker socket/config/host filesystem binds are not execution targets", () => {
  const data = fixture();
  data[6].Mounts = [
    { Type: "bind", Destination: "/var/run/docker.sock", RW: false },
  ];
  assert.throws(() => validateContainers(data), /InvalidLab/);
});
