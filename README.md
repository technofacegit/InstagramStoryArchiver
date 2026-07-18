# Instagram Story Archiver

.NET 8 worker that periodically checks a small set of monitored Instagram accounts (max 10–15) for new Stories and archives images/videos to local disk.

## 1. Projenin amacı

Kendi Instagram oturumunuz üzerinden, önceden belirlenmiş kullanıcıların aktif Story paylaşımı olup olmadığını kontrol eder. Yeni Story bulunduğunda medyayı indirir, SQLite veritabanına kaydeder ve aynı Story’yi tekrar indirmez.

## 2. Yasal ve platform kullanım şartları uyarısı

Bu araç **kişisel arşiv / eğitim amaçlı** bir MVP’dir.

- Instagram’ın [Terms of Use](https://help.instagram.com/) ve otomasyon politikalarına aykırı kullanım hesap kısıtlamasına yol açabilir.
- Otomasyon Instagram tarafından her zaman desteklenmeyebilir; hesap kısıtlama riski vardır.
- Yalnızca erişim hakkınız olan içerikleri ve kendi hesabınızı kullanarak çalıştırın.
- CAPTCHA kırma, rate-limit bypass, proxy rotasyonu veya saldırgan login denemeleri **bilinçli olarak desteklenmez**.

Kullanım sorumluluğu size aittir.

## 3. Gereksinimler

- .NET 8 SDK
- PowerShell (`pwsh`) veya Playwright CLI (browser kurulumu için)
- Chromium (Playwright tarafından kurulur)
- (Opsiyonel) Docker / Docker Compose

## 4. Kurulum

```bash
git clone https://github.com/technofacegit/InstagramStoryArchiver.git
cd InstagramStoryArchiver
dotnet restore
dotnet build
./scripts/setup-playwright.sh
```

## 5. Playwright browser kurulumu

```bash
dotnet build src/InstagramStoryArchiver.Infrastructure/InstagramStoryArchiver.Infrastructure.csproj
pwsh src/InstagramStoryArchiver.Infrastructure/bin/Debug/net8.0/playwright.ps1 install chromium
```

Alternatif:

```bash
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

## 6. İlk Instagram login işlemi

Login **headful** (görünür tarayıcı) ile lokal makinede yapılmalıdır:

```bash
cd src/InstagramStoryArchiver.Worker
dotnet run -- login
```

1. Chromium açılır ve Instagram login sayfasına gider.
2. Kullanıcı adı / şifre / 2FA / checkpoint adımlarını **manuel** tamamlayın.
3. Ana sayfa göründüğünde terminale dönüp **Enter** basın.
4. Oturum `data/instagram-storage-state.json` dosyasına kaydedilir.

> **Güvenlik:** Bu dosya Instagram hesabınıza erişim sağlayabilecek hassas oturum verisi içerir. Git’e eklenmez. Dosya izinlerini mümkün olduğunca yalnızca kendi kullanıcınızın okuyabileceği şekilde bırakın.

## 7. Takip edilen kullanıcı ekleme

```bash
dotnet run --project src/InstagramStoryArchiver.Worker -- users add exampleuser
dotnet run --project src/InstagramStoryArchiver.Worker -- users add @ExampleUser
dotnet run --project src/InstagramStoryArchiver.Worker -- users list
dotnet run --project src/InstagramStoryArchiver.Worker -- users enable exampleuser
dotnet run --project src/InstagramStoryArchiver.Worker -- users disable exampleuser
dotnet run --project src/InstagramStoryArchiver.Worker -- users remove exampleuser
```

`remove` / `disable` geçmiş Story kayıtlarını silmez; yalnızca takibi pasifleştirir.
Username `@` ve büyük/küçük harf farkı normalize edilir (`exampleuser`).

## 8. Worker çalıştırma

```bash
dotnet run --project src/InstagramStoryArchiver.Worker
```

Varsayılan polling aralığı **15 dakika**. Kullanıcılar sırayla kontrol edilir; aralarında 20–40 sn rastgele bekleme vardır.

## 9. Manuel tek kullanıcı kontrolü

```bash
dotnet run --project src/InstagramStoryArchiver.Worker -- users check exampleuser
dotnet run --project src/InstagramStoryArchiver.Worker -- check-all
```

## 10. Docker ile çalıştırma

1. Lokal host’ta `dotnet run -- login` ile storage state üretin.
2. `data/instagram-storage-state.json` dosyasını güvenli şekilde deployment ortamına kopyalayın.
3. Çalıştırın:

```bash
docker compose up -d --build
```

Volume’lar:

- `/app/data` → DB + storage state + lock
- `/app/archive` → indirilen medya
- `/app/logs` → Serilog dosyaları

**Exit code davranışı**

| Kod | Anlam |
|-----|--------|
| 0 | Normal kapanış |
| 1 | Genel hata / başka instance çalışıyor |
| 2 | Session expired / challenge — **manuel login gerekli** |

`restart: unless-stopped` kullanılır. Session expired olduğunda uygulama döngüye girmeden durur; storage state yenilenmeden container’ı zorla ayakta tutmayın.

## 11. Veritabanı konumu

SQLite:

```text
data/instagram-story-archiver.db
```

Migration’lar uygulama başlangıcında uygulanır.

## 12. Arşiv klasörü yapısı

```text
archive/{username}/{yyyy}/{MM}/{dd}/{HHmmss}_{storyKey}.{ext}
```

Örnek:

```text
archive/exampleuser/2026/07/18/184530_ABC123.mp4
```

İndirme önce `.tmp` dosyasına yapılır, başarıdan sonra atomik taşınır.

## 13. Log dosyaları

```text
logs/instagram-story-archiver-.log
```

Console + rolling file. Şifre, cookie, token ve hassas header’lar loglanmaz. Medya URL’leri kısaltılır.

## 14. Session expired olduğunda yapılacaklar

1. Worker/container’ın durduğunu doğrulayın (exit code 2).
2. Lokal olarak `dotnet run -- login` çalıştırın.
3. Yeni `data/instagram-storage-state.json` dosyasını ortama kopyalayın.
4. Worker’ı yeniden başlatın.

Uygulama otomatik agresif login denemesi **yapmaz**.

## 15. Instagram arayüzü değiştiğinde locator güncelleme

Tüm DOM locator’ları tek yerde toplanmıştır:

```text
src/InstagramStoryArchiver.Infrastructure/Playwright/InstagramLocators.cs
```

Fragile CSS class’ları yerine `href`, `role`, `aria-label` tercih edilir.
Network JSON tarama mantığı:

```text
src/InstagramStoryArchiver.Infrastructure/Playwright/InstagramStoryResponseParser.cs
```

## 16. Sorun giderme

| Belirti | Olası çözüm |
|---------|-------------|
| Storage state missing | `dotnet run -- login` |
| Login page detected | Storage state yenile |
| Challenge/checkpoint | Instagram’da manuel çöz, sonra login |
| Navigation timeout | Ağı / Instagram erişimini kontrol et; timeout ayarını artır |
| Playwright browser missing | `scripts/setup-playwright.sh` |
| Another instance running | `data/worker.lock` dosyasını kontrol et |
| No stories detected | Locator’ları güncelle; kullanıcının gerçekten aktif Story’si var mı bak |

## 17. Güvenlik önerileri

- Storage state, `.db`, `archive/`, `logs/`, `.env` Git’e eklenmez.
- Instagram şifresini config veya DB’de tutmayın.
- Storage state dosyasını paylaşmayın; sızdırılırsa oturumu Instagram’dan kapatın.
- Tek instance çalıştırın (lock file).
- Production loglarında medya URL query string’lerini sanitize edin (uygulama bunu yapar).

## 18. Bilinen kısıtlamalar

- Resmi Instagram API kullanılmaz; UI + network response scraping kırılgan olabilir.
- Instagram DOM / JSON şeması değişebilir.
- Yalnızca düşük hacimli kullanım için tasarlanmıştır (max ~15 kullanıcı).
- Rate-limit bypass / captcha çözümü yoktur.
- Story viewer DOM’u değişirse locator güncellemesi gerekebilir.
- Close friends / özel Story senaryoları hesap izinlerine bağlıdır.

---

## Mimari

```text
InstagramStoryArchiver.sln
src/
  InstagramStoryArchiver.Worker/
  InstagramStoryArchiver.Application/
  InstagramStoryArchiver.Domain/
  InstagramStoryArchiver.Infrastructure/
tests/
  InstagramStoryArchiver.Tests/
```

## Testler

```bash
dotnet test
```

Instagram’a gerçek bağlantı yapan testler varsayılan suite’te yoktur.
