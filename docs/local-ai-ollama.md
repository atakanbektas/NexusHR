# Yerel AI CV Analizi — Docker Ollama

Bu opt-in profil, Candidate AI modülünü gerçek bir yerel modele bağlar. OpenAI adapter'ı korunur. Yerel analiz için API key, Windows Ollama/Python/CUDA kurulumu veya ücretli AI aboneliği gerekmez. Harici AI API ücreti **0 USD**; CPU, RAM, disk ve elektrik tüketimi vardır.

## Başlatma — Windows PowerShell

Docker Desktop Linux containers modunda çalışmalı. Repository kökünde:

```powershell
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai config --quiet
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai up -d --build --wait --wait-timeout 1200
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai ps
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai exec ollama ollama list
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai exec ollama ollama show qwen3:4b
```

CLI eski terminalin PATH'inde görünmüyorsa yeni PowerShell açın. Gerekirse mevcut kullanıcı kurulumunda:

```powershell
$env:Path = "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin;$env:Path"
```

İlk başlangıç resmi imajı ve yaklaşık 2,5 GB model dosyasını indirir; `--wait-timeout 1200` bunun içindir. İkinci başlangıçta bootstrap `ollama show` ile kontrol eder ve mevcut modeli yeniden indirmez. Model `ollama-models` named volume'ünde tutulur. Veritabanı veya eski volume silinmez.

Profil, Candidate API için `Enabled=true`, `Provider=Ollama`, `AllowExternalProcessing=false`, model/endpoint ve CPU'ya uygun limitleri otomatik uygular. Normal `docker-compose.yml` tek başına kullanıldığında AI önceki gibi kapalıdır. Compose profili tek başına backend environment değiştiremeyeceği için **iki `-f` dosyası da gereklidir**.

## Kullanıcı arayüzü

1. http://localhost:5173 adresini açın. Geliştirme fixture'ı: `hr.specialist` / `NexusHR123!`; bunlar production kimlik bilgileri değildir.
2. Bir aday oluşturup okunabilir PDF CV yükleyin veya CV'si olan adayı açın.
3. **CV Analizi** sekmesinde **Yerel AI — Ollama** ve **Harici API maliyeti: 0 USD** görünür.
4. Model hazırsa **Analiz başlat** kullanılabilir. Kuyrukta/Çalışıyor durumunu takip edin. İlk çağrı model yüklenmesi nedeniyle daha yavaş olabilir.
5. Profesyonel özet, teknik beceriler, deneyim, eğitim, sertifikalar ve dilleri; sayfa/alıntı kanıtlarıyla inceleyin. Kaynakta bulunmayan alanlar uyarıyla gösterilir.
6. HR onay/ret/düzeltme notu kaydeder. Sonuç aday doğrulamasını, Hiring veya Onboarding kararını otomatik değiştirmez.

İletişim bilgileri gizlendikten sonra metin kalmazsa CV modele gönderilmez; **Doğrulanmış sonuç yok** (`NoFindings`) gösterilir. Okunabilir metne rağmen model sıfır kanıtlı bilgi döndürürse de bu, başarılı analiz sayılmaz. Görsel/tarama PDF için OCR eksikliği açıkça bildirilir. Eski boş `Succeeded` kayıtları audit geçmişinde korunur; ekranda **Eski boş analiz** olarak gösterilir ve onaylanamaz. Aynı CV için **Analiz başlat** yeni bir kayıt açar; eksik profesyonel bilgi uydurulmaz. [Kök neden ve yeniden deneme adımları](audits/empty-cv-analysis-fix.md).

Özet de kaynakta bulunan profesyonel ifadenin alıntısıdır; uydurma serbest anlatım oluşturulmaz. Model bir adayın CV'sinde yazanların doğruluğunu veya işe uygunluğunu kanıtlamaz.

HrManager ve Auditor yalnızca okuyabilir. DepartmentManager, IT ve Admin'e örtük CV erişimi verilmez. Backend yetkilendirmesi asıldır.

## Sabitlenen runtime ve model

- Resmi imaj: `ollama/ollama:0.40.12`, manifest digest `sha256:20d1bbfbabe1c97ba4b16301f08a20d8484bcf77047742a3cb71c613d8e12d0b`.
- Varsayılan model: `qwen3:4b`, Q4_K_M; indirilmiş manifest digest `359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7`.
- Native `/api/chat`, JSON Schema `format`, `stream:false`, `think:false`, temperature 0. Modelin gerçek container davranışı opt-in testlerle doğrulanır.
- Yanıt kaydındaki provider request ID model digest'ini içerir. Tag'ler değişebilir; `ollama list` ve `/api/tags` ile yerel sürümü kontrol edin.

## Kaynak sınırları ve yapılandırma

CPU üzerinde çalışır; GPU zorunlu değildir. Docker'a yaklaşık **16 GB RAM, en az 4 CPU** ve imaj/model için yeterli ek disk ayırmak önerilir. Inference container sınırı 8 GB/6 CPU, model thread sayısı 4'tür. Model dışında NexusHR servisleri ve build işlemleri de kaynak tüketir. Küçük makinelerde cold start/analiz süresi uzar; başarı/süre garantisi değildir. GPU yolu bu değişiklikte doğrulanmamıştır.

| Backend ayarı | Yerel profil değeri |
|---|---|
| `CvAnalysis__Provider` | `Ollama` (`OpenAI` geriye uyumlu varsayılan) |
| `CvAnalysis__Enabled` | `true` |
| `CvAnalysis__AllowExternalProcessing` | `false`; yerel çalışmada harici izin/anahtar istenmez |
| `CvAnalysis__Model` | `${OLLAMA_MODEL:-qwen3:4b}` |
| `CvAnalysis__Ollama__BaseUrl` | `http://nexushr-local-ai:11434` |
| `CvAnalysis__Ollama__ContextLength` | 16384 |
| `CvAnalysis__Ollama__Threads` | 4 |
| `CvAnalysis__TimeoutSeconds` | 240 (yerel için en fazla 600) |
| `CvAnalysis__MaxOutputTokens` | 4000 |
| `CvAnalysis__MaxParallelCalls` | 1; doğrulanan aralık 1–2 |
| Günlük kullanıcı kotası | 20 rezervasyon/istek; tekrarlar da sayılır |
| Toplam deneme sınırı | 2, sınırsız retry yok |

Girdi karakter sınırı ve konservatif UTF-8 byte/bağlam sınırı birlikte uygulanır; uzun CV sessizce kesilmez. Yerel sonuçlarda rezervasyon/harici kullanım maliyeti sıfırdır; ücretli bütçe satırı açılmaz. Kullanıcı kotası korunur. OpenAI için mevcut pozitif fiyat, USD rezervasyon ve belirsiz ücret kuralları aynen sürer. Başka sağlayıcıya/model ayarına geçildiğinde eski kuyruğun farklı sağlayıcı üzerinden çalıştırılmasına izin verilmez.

## Ağ ve gizlilik

İki ayrı aşama vardır:

- `ollama-model-init`: yalnızca model indiren, CV almayan kısa ömürlü bootstrap. İnternete erişebilir, HTTP sunucusu yalnızca kendi loopback adresinde dinler.
- `ollama`: yalnızca `internal:true` Docker ağına bağlı inference sunucusu. Varsayılan **host portu yoktur**, internet çıkışı yoktur, `OLLAMA_NO_CLOUD=1` kullanır. Candidate bu ağa eklenir; mevcut servis ağı korunur. Özel `nexushr-local-ai` alias'ı başka mevcut Ollama container'larıyla DNS çakışmasını önler.

Adapter yalnızca bu yerel Docker adlarını veya loopback endpoint'lerini kabul eder; redirect takip etmez. Cloud model adları/metadata'sı reddedilir. OpenAI'ye fallback yoktur. CV/prompt/result loglanmaz. Metin güvenilmeyen veri sayılır; tool execution/web çağrısı yoktur. Bilinen iletişim bilgileri temizlenir; bu anonimleştirme garantisi değildir.

Model alıntısı verilen sayfada **birebir** aranır ve ilk kesin eşleşmenin UTF-16 konumu hesaplanır. Ardından mevcut EvidenceValidator sayfa/span/alıntı/değer kontrolünü tekrar yapar. Modelden hataya açık karakter sayma istenmez; bulanık eşleştirme, eksik JSON'u tamamlama veya uydurma kanıt kabul edilmez.

## Sorun giderme ve yeniden başlatma

```powershell
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai logs --tail 80 ollama-model-init ollama
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai logs --tail 80 candidate-api
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai exec ollama ollama list
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai restart ollama candidate-api
```

Model indirilmemiş/sunucu kapalıysa history yanıtı `ready=false` ve Türkçe açıklama içerir; yeni analiz 503 ile reddedilir ve ücret/kota ayrılmaz. Model daha sonra kaybolursa worker kontrollü hata kaydeder. Hatalı JSON/kanıt, timeout ve incomplete response sonuç olarak gösterilmez. Bağlantı/meşgul hataları sınırlı yeniden denenebilir. İptal queued işi durdurur; yerel running çağrıda DB cancellation işareti takip edilerek HTTP cancellation gönderilir. Worker çökmesinden kalan yerel lease'ler sıfır maliyetli Failed olur; ücret belirsizliği onayı gerektirmez. OpenAI recovery davranışı korunur.

Model değiştirmek için eski modeli silmeden:

```powershell
$env:OLLAMA_MODEL = 'qwen3:4b-instruct-2507-q4_K_M'
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai up -d --build --wait --wait-timeout 1200
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai exec ollama ollama list
# Varsayılana dönmek için:
Remove-Item Env:OLLAMA_MODEL -ErrorAction SilentlyContinue
```

Bu alternatif için otomatik kalite eşdeğerliği iddia edilmez; model değiştirince sentetik testleri yeniden çalıştırın.

Yalnızca yerel AI'yı durdurmak ve yeniden açmak:

```powershell
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai stop ollama
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai up -d --wait ollama
```

AI'yı kapatıp ana uygulamayı açık bırakmak:

```powershell
docker compose -f docker-compose.yml up -d --no-deps candidate-api
```

**Volume silmeyin, `down -v`, prune veya veritabanı reset'i kullanmayın.** Mevcut ayrı Ollama/diğer container'lar otomatik silinmez/değiştirilmez.

## Testler

Normal CI deterministic fake/HTTP contract/PostgreSQL/RabbitMQ testlerini çalıştırır; model indirme ve inference zorunlu değildir. Gerçek testler açıkça başlatılır. Backend/Playwright test geliştirmesi ayrıca .NET 10 ve Node 24/npm gerektirir; normal Docker uygulama kullanımı bunların Windows'a kurulumunu gerektirmez.

Host'tan native testler için isteğe bağlı **yalnızca loopback 11435** (11434'te mevcut sunucu varsa dokunulmaz). İç ağa kapalı inference sunucusu doğrudan publish edilmez; test dosyası yalnızca version/tags/show/chat yollarını aktaran, access log'u kapalı küçük bir test proxy'si ekler. Proxy normal uygulama profilinde yoktur ve test bitince durdurulur:

```powershell
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai-test.yml --profile ai up -d --wait ollama-test-proxy
$env:RUN_OLLAMA_TESTS = '1'
$env:OLLAMA_TEST_URL = 'http://127.0.0.1:11435'
dotnet test NexusHR.Candidate.IntegrationTests --configuration Release --filter FullyQualifiedName~OllamaRuntimeTests
Push-Location NexusHR.Web
npm ci
npx playwright install chromium
npm run test:e2e -- ollama-real.spec.ts
Pop-Location
Remove-Item Env:RUN_OLLAMA_TESTS -ErrorAction SilentlyContinue
Remove-Item Env:OLLAMA_TEST_URL -ErrorAction SilentlyContinue
# Test proxy portunu kapat, modeli/data'yı koru:
docker compose -f docker-compose.yml -f docker-compose.ai.yml -f docker-compose.ai-test.yml --profile ai stop ollama-test-proxy
docker compose -f docker-compose.yml -f docker-compose.ai.yml --profile ai up -d --wait ollama
```

Gerçek E2E sentetik Türkçe HTML'den Chromium ile PDF üretir, mevcut formdan çıkarır/yükler, gerçek API/Outbox/Inbox/RabbitMQ/worker/Ollama üzerinden analiz eder; SQL ile Candidate PostgreSQL kaydını, HR review/history'yi ve container restart sonrasında kalıcılığı doğrular. `test-results/ollama-real-result.json` yalnızca sentetik test artefact'ıdır ve Git dışında tutulur. Gerçek kişisel CV kullanmayın.

GitHub'da `Ollama real-model validation (manual)` workflow'u aynı opt-in doğrulamayı ayrı bir hosted runner'da yapar. Normal `verify` işine büyük model indirmesi eklenmemiştir. Hosted compute/disk sınırları ve Actions kullanım maliyeti harici AI API ücretiyle aynı şey değildir; yerel model kullanımı ücretli LLM endpoint'i çağırmaz.

Resmi sözleşmeler: [Ollama Docker](https://docs.ollama.com/docker), [native chat API](https://docs.ollama.com/api/chat), [structured outputs](https://docs.ollama.com/capabilities/structured-outputs), [qwen3:4b model kaydı](https://ollama.com/library/qwen3:4b).
