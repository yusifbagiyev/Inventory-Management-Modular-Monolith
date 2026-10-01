# Cloudflare Tunnel ilə public giriş (https://inventory.az)

Proqram internetə **Cloudflare Tunnel** ilə çıxır: serverdəki `cloudflared` konteyneri Cloudflare-ə
**çıxan** bağlantı açır, ona görə serverdə və firewall-da heç bir port açılmır, public IP lazım deyil.
TLS (sertifikat) Cloudflare-dədir.

```
istifadəçi ─https─> Cloudflare (inventory.az) ─tunel─> cloudflared ─http─> nginx:8080 ─> app
LAN:        nginx:80/443 (10.0.1.60) ─> yalnız https://inventory.az-a yönləndirir; inventory166.az açılmır
```

- `nginx:8080` yalnız tunel üçündür, hosta publish olunmur. Ziyarətçinin IP-si Cloudflare-in
  `CF-Connecting-IP` başlığından götürülür, ona görə login limiti, audit jurnalı və loglar real IP-ni görür.
- Tunel yalnız proqramı açır. Seq (5342), PostgreSQL (5432) və ServiceDesk API (5001) əvvəlki kimi
  yalnız LAN-dadır.
- Aşağıda `/opt/inventory166` layihə qovluğudur.

## 1. Domen Cloudflare-də

1. `inventory.az` domeni Cloudflare hesabına əlavə olunur (*Add a site*, Free plan kifayətdir).
2. Cloudflare-in verdiyi iki nameserver `.az` domen qeydiyyatçısında (registrar) domenin
   nameserver-ləri kimi yazılır. Domen *Active* olana qədər gözləyin (bir neçə dəqiqədən bir neçə saata qədər).

## 2. Tuneli yaratmaq

1. Cloudflare → **Zero Trust** → *Networks* → **Tunnels** → *Create a tunnel* → **Cloudflared**.
2. Ad: `inventory166`. Növbəti səhifədə göstərilən əmrdə `--token`-dan sonrakı uzun dəyəri kopyalayın.
   Bu **tokendir** — şifrə kimi saxlayın, heç yerə yazmayın (yalnız serverin `.env`-inə).
   Göstərilən quraşdırma əmrlərini işlətməyin: `cloudflared` docker-compose-da artıq var.
3. **Public Hostname** əlavə edin:
   - Subdomain: boş, Domain: `inventory.az`, Path: boş
   - Service: Type **HTTP**, URL **`nginx:8080`**
   - (istəsəniz ikinci hostname: `www` → eyni `HTTP` / `nginx:8080`)

## 3. Serverdə

`.env`-ə iki sətir əlavə edin (fayl `github-runner`-indir, `chmod 600` qalmalıdır):

```bash
sudo -u github-runner nano /opt/inventory166/.env
```

```
COMPOSE_PROFILES=tunnel
CLOUDFLARE_TUNNEL_TOKEN=<2-ci addımdakı token>
```

Sonra tuneli işə salın. Bu dəyişiklik master-ə push olunub deploy bitdikdən **sonra** (compose-da
`cloudflared` servisi o zaman görünür) — deploy özü də `docker compose up -d` edir, ona görə `.env`
deploydan əvvəl yazılıbsa, tunel avtomatik qalxır. Əl ilə:

```bash
cd /opt/inventory166 && sudo -u github-runner docker compose up -d
```

Yoxlama:

```bash
docker logs --tail 20 inventory_cloudflared     # "Registered tunnel connection" x4
```

Zero Trust → Tunnels-də tunel **Healthy** görünməlidir; https://inventory.az açılmalıdır.

## 4. Cloudflare ayarları (inventory.az zonası)

| Harada | Ayar | Niyə |
|---|---|---|
| SSL/TLS → Edge Certificates | **Always Use HTTPS**: On | http ilə gələnlər https-ə keçsin |
| Scrape Shield | **Email Address Obfuscation**: Off | yerində yenilənən siyahılarda e-poçtlar `[email protected]` kimi görünür |
| Speed → Optimization | **Rocket Loader**: Off | skriptlərin ardıcıllığını pozur |
| Network | **WebSockets**: On (default) | canlı yeniləmələr və bildirişlər (SignalR) |

## 5. Təhlükəsizlik

Proqram internetdən görünür. Proqramın özündə:

- Giriş: səhv cəhdlərin hamısına eyni cavab verilir (istifadəçi adı tapılmır); hesab 10 səhvdən sonra
  15 dəqiqəlik bağlanır; bir ünvandan 15 dəqiqədə 20 uğursuz cəhddən sonra o ünvan gözləyir
  (yalnız uğursuzlar sayılır — ofis bir ünvandan girir).
- Şifrə ən azı 10 simvol, böyük və kiçik hərf, rəqəm. Şifrə dəyişəndə/sıfırlananda və hesab
  deaktiv olanda istifadəçinin digər sessiyaları 5 dəqiqə ərzində bitir; istənilən sessiya 30 gündən sonra.
- Şəkillər yüklənəndə yoxlanır (əsl JPG/PNG, ≤50 MP) və içindəki məlumat (telefonun GPS yeri, cihaz)
  silinir; köhnə şəkillər deploydan bir dəqiqə sonra bir dəfə təmizlənir. Cloudflare köhnə şəkilləri
  keşləyibsə: **Caching → Configuration → Purge Everything** (bir dəfə, təmizlikdən sonra).
- ServiceDesk açarı yalnız LAN-da (5001) və yalnız məhsul API-sində işləyir.

### Cloudflare Access (əlavə qat: sayta girməzdən əvvəl e-poçt kodu)

Zero Trust → **Access → Applications → Add an application → Self-hosted**:

1. Application name: `Inventory`; **Public hostname**: `inventory.az` (istəsəniz ikinci: `www.inventory.az`).
2. Session duration: `1 month` (telefonda hər gün kod istəməsin).
3. **Policy** əlavə edin: Action **Allow**, Include → **Emails ending in** → `@166.az`
   (və ya yalnız konkret ünvanlar: **Emails** → siyahı).
4. Login methods: **One-time PIN** (e-poçta kod gəlir; əlavə quraşdırma lazım deyil).
5. Saxlayın. İndi `inventory.az` açılanda əvvəl Cloudflare e-poçt soruşur, gələn kodu yazdıqdan sonra
   proqramın öz giriş səhifəsi açılır.

Qeyd: Access yalnız `inventory.az`-a aiddir; ServiceDesk (LAN, 5001) təsirlənmir. Free plan 50 istifadəçiyə qədərdir.
Problem olsa, tətbiqi Access-də silmək kifayətdir — proqram əvvəlki kimi açılır.

## Qeydlər

- Proqrama yalnız `https://inventory.az` ilə girilir. Serverin IP-si (`10.0.1.60`) LAN-da yalnız
  `https://inventory.az`-a yönləndirir (yol saxlanılır); `www.inventory.az` da `inventory.az`-a. Köhnə
  `inventory166.az` ümumiyyətlə açılmır (nginx bağlantını qəbul etmir); onun DNS qeydini də silmək olar. İnternet və ya Cloudflare olmayanda proqram açılmır. ServiceDesk (5001) dəyişmir.
- WhatsApp mesajları dəyişmir (şəkil faylın özü göndərilir, link yox).
- Tuneli söndürmək: `.env`-dən `COMPOSE_PROFILES=tunnel`-i silin və
  `docker compose stop cloudflared && docker compose rm -f cloudflared`. Tamamilə ləğv: Zero Trust-da tuneli silin.
- Token sızıbsa: Zero Trust-da tunelin tokenini yeniləyin (və ya tuneli silib yenisini yaradın), yeni tokeni `.env`-ə yazıb `docker compose up -d`.
