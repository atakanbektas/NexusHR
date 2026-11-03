// Independent, bounded cleanup survives a killed agent. No business project/volumes are accepted.
import {
  containers,
  recoverLab,
  configureModel,
  command,
  sleep,
} from "./lab.mjs";
const [id, seconds] = process.argv.slice(2);
if (
  !/^[a-f0-9-]{36}$/.test(id ?? "") ||
  !/^\d+$/.test(seconds ?? "") ||
  +seconds < 1 ||
  +seconds > 900
)
  process.exit(2);
await sleep(+seconds * 1000);
try {
  await containers();
  const active = (
    await command([
      "ps",
      "-q",
      "--filter",
      `label=nexushr.engineering.run=${id}`,
      "--filter",
      "label=nexushr.engineering.disposable=true",
    ])
  )
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  for (const c of active)
    if (/^[a-f0-9]{12,64}$/.test(c)) await command(["stop", "--time", "2", c]);
  await recoverLab();
  await configureModel("qwen3:4b");
} catch {
  process.exitCode = 1;
} // Host/container unavailability is still a reported recovery failure, never a guarantee.
