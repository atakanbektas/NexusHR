import { Alert, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";

export type EvidenceFact = { category: string; value: string; page: number; start: number; quote: string; source?: string;
  fields?: { name: string; value: string; quote: string }[] | null; regions?: { x: number; y: number; width: number; height: number }[] };
export default function CvEvidenceTables({ facts, months, reviews = [] }: { facts: EvidenceFact[]; months?: number | null;
  reviews?: { factIndex?: number | null; decision: string; correctedValue?: string | null }[] }) {
  const tables = [
    { category: "employment", title: "İş deneyimi tablosu", columns: [["company", "Şirket"], ["position", "Pozisyon"], ["start", "Başlangıç"], ["end", "Bitiş"], ["duration", "Süre"]] },
    { category: "skill", title: "Teknik beceriler tablosu", columns: [["value", "Teknoloji"], ["category", "Kategori"], ["quote", "CV’deki kanıt"]] },
    { category: "education", title: "Eğitim tablosu", columns: [["institution", "Kurum"], ["department", "Bölüm"], ["degree", "Derece"], ["date", "Tarih"]] },
    { category: "language", title: "Dil tablosu", columns: [["value", "Dil"], ["level", "Seviye"], ["quote", "CV’deki kanıt"]] },
  ];
  return <Stack spacing={2}>
    <Alert severity="info">Toplam deneyim için belgelenen takvim ayı kapsamı: {months == null ? "Hesaplanamadı" : `${months} ay`}.
      Yalnızca açık ISO tarih aralıkları kullanılır; çakışmalar bir kez sayılır. Bu, kesin çalışma süresi değildir; eksik ve açık uçlu tarihlerden süre uydurulmaz.</Alert>
    {tables.filter(table => facts.some(f => f.category === table.category)).map(table => <Stack key={table.category}>
      <Typography variant="h6">{table.title}</Typography>
      <TableContainer><Table size="small"><TableHead><TableRow>{table.columns.map(([name, label]) => <TableCell key={name}>{label}</TableCell>)}<TableCell>Kaynak</TableCell><TableCell>İnsan incelemesi</TableCell></TableRow></TableHead>
        <TableBody>{facts.filter(f => f.category === table.category).map((fact, i) => <TableRow key={i}>{table.columns.map(([name]) => <TableCell key={name} sx={{ whiteSpace: "pre-wrap" }}>
          {name === "value" ? fact.value : name === "quote" ? fact.quote : fact.fields?.find(f => f.name === name)?.value ?? "Belirtilmedi"}
        </TableCell>)}<TableCell>Sayfa {fact.page} · {fact.source ?? "metin"}</TableCell><TableCell>{(() => {
          const review = reviews.find(r => r.factIndex === facts.indexOf(fact) || r.factIndex == null);
          return review ? `${({ approve: "Kabul edildi", reject: "Reddedildi", correct: "Düzeltildi" } as Record<string, string>)[review.decision] ?? review.decision}${review.correctedValue ? ` — ${review.correctedValue} (orijinal değer korundu)` : ""}` : "İnceleme bekliyor";
        })()}</TableCell></TableRow>)}</TableBody>
      </Table></TableContainer>
    </Stack>)}

  </Stack>;
}
