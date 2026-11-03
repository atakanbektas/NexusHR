import { command, labRequest } from "./lab.mjs";
export async function createSentinel(id) {
  const value = {
    firstName: "Synthetic",
    lastName: "Sentinel",
    email: `sentinel-${id}@example.test`,
    phoneNumber: "05555555555",
  };
  const created = await labRequest("", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(value),
  });
  if (!/^[a-f0-9-]{36}$/.test(created.id)) throw new Error("InvalidLab");
  return { ...value, id: created.id };
}
export async function sentinelPreserved(sentinel) {
  try {
    const current = await labRequest(`/${sentinel.id}`);
    return ["id", "firstName", "lastName", "email", "phoneNumber"].every(
      (key) => current[key] === sentinel[key],
    );
  } catch {
    return false;
  }
}
function pdf() {
  const text =
    "BT /F1 12 Tf 50 750 Td (Synthetic Sentinel) Tj 0 -20 Td (C# PostgreSQL Docker Engineer) Tj ET";
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    `<< /Length ${text.length} >>\nstream\n${text}\nendstream`,
  ];
  let result = "%PDF-1.4\n";
  const offsets = [];
  for (let i = 0; i < objects.length; i++) {
    offsets.push(result.length);
    result += `${i + 1} 0 obj\n${objects[i]}\nendobj\n`;
  }
  const xref = result.length;
  result +=
    "xref\n0 6\n0000000000 65535 f \n" +
    offsets.map((n) => `${String(n).padStart(10, "0")} 00000 n \n`).join("");
  return Buffer.from(
    result + `trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`,
    "ascii",
  );
}
export async function verifyDuringOutage(sentinel) {
  const upload = new FormData();
  upload.append(
    "file",
    new Blob([pdf()], { type: "application/pdf" }),
    "synthetic-sentinel.pdf",
  );
  await labRequest(`/${sentinel.id}/documents/cv`, {
    method: "POST",
    body: upload,
  });
  await labRequest(`/${sentinel.id}/documents/verify`, { method: "POST" });
}
export async function messageDelivered(cs, sentinel) {
  const db = cs.find(
    (c) => c.Config.Labels["com.docker.compose.service"] === "hiring-db",
  );
  const result = await command([
    "exec",
    db.Id,
    "psql",
    "-U",
    "postgres",
    "-d",
    "nexushr_hiring",
    "-Atc",
    `SELECT count(*) FROM eligible_candidates WHERE "CandidateId"='${sentinel.id}'::uuid`,
  ]);
  return result.trim() === "1";
}
