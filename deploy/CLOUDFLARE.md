# Cloudflare Tunnel ilə public giriş (https://inventory.az)

Proqram internetə **Cloudflare Tunnel** ilə çıxır: serverdəki `cloudflared` konteyneri Cloudflare-ə
**çıxan** bağlantı açır, ona görə serverdə və firewall-da heç bir port açılmır, public IP lazım deyil.
TLS (sertifikat) Cloudflare-dədir.

```
istifadəçi ─https─> Cloudflare (inventory.az) ─tunel─> cloudflared ─http─> nginx:8080 ─> app
LAN:        ─https─> nginx:443 (inventory166.az, 10.0.1.60) ─> app            (dəyişmir)
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

Proqram artıq internetdən görünür:

- Bütün hesabların parolu güclü olsun; lazımsız/köhnə hesabları deaktiv edin.
  Hazırda hamı **Admin**-dir — lazım olmayanları **User** edin.
- Login həm nginx-də (dəqiqədə 10, IP üzrə), həm proqramda (10 dəqiqədə 5, IP üzrə) məhdudlaşdırılıb.
  Əlavə olaraq Cloudflare → Security → WAF → *Rate limiting rules* ilə `/Account/Login` üçün qayda qoymaq olar.
- Daha sərt variant: **Cloudflare Access** (Zero Trust → Access → Applications) — sayta girməzdən
  əvvəl Cloudflare şirkət e-poçtuna kod göndərir; yalnız icazə verilən ünvanlar keçir.
- Məhsul şəkilləri (`/images/...`) indi də LAN-da olduğu kimi linklə girişsiz açılır; adları
  təsadüfidir (tarix + GUID), ona görə tapılmaz, amma link paylaşılarsa açılar.

## Qeydlər

- `inventory.az` və `inventory166.az` ayrı domenlərdir: istifadəçi hər birində ayrıca daxil olur.
- WhatsApp mesajları dəyişmir (şəkil faylın özü göndərilir, link yox).
- Tuneli söndürmək: `.env`-dən `COMPOSE_PROFILES=tunnel`-i silin və
  `docker compose stop cloudflared && docker compose rm -f cloudflared`. Tamamilə ləğv: Zero Trust-da tuneli silin.
- Token sızıbsa: Zero Trust-da tunelin tokenini yeniləyin (və ya tuneli silib yenisini yaradın), yeni tokeni `.env`-ə yazıb `docker compose up -d`.
