# Mikroservislərdən modular monolitə keçid (production)

Serverdəki köhnə 11 konteynerli sistem yeni tək `app` konteynerinə keçirilir. Köhnə sistemdə 5 servis,
Ocelot gateway, ayrıca Web, RabbitMQ, Postgres, Seq və nginx var. Data 5 bazadan bir `inventory` bazasına
köçür, hər modul üçün ayrıca schema ilə:

| Köhnə baza | Yeni schema |
|---|---|
| `identity_service` | `identity` |
| `product_service` | `product` |
| `route_service` | `route` |
| `approval_service` | `approval` |
| `notification_service` | `notification` |

- **Köhnə bazalar yalnız oxunur.** Geri qayıtmaq bir əmrdir (bax: *Geri qaytarma*).
- Hər şeyi `deploy/cutover.sh` edir: yoxlama, ehtiyat nüsxə, keçid, data köçürmə, sayların müqayisəsi.
  Hər addımda xəta olarsa, dayanır və nə etməli olduğunuzu yazır.
- Serverdə qovluq: `/opt/inventory166`. Əmrlər **root** ilə icra olunur.

## 0. Hazırlıq (dayanma yoxdur, istənilən vaxt)

1. **`.env`** (`/opt/inventory166/.env`):
   - `SERVICEDESK_API_KEY` — ServiceDesk-in açarı (`X-Api-Key`). Köhnə açar `.env`-ə yazılıb (2026-09-30).
     Açarda `$` işarəsi var, ona görə dəyər **tək dırnaqda** olmalıdır: `SERVICEDESK_API_KEY='...'`.
     ServiceDesk köhnə product-service-ə birbaşa `http://10.0.1.60:5001` ünvanı ilə müraciət edir.
     Yeni sistemdə həmin port nginx-dədir və yalnız product API-ni (`/api/products`, `/api/categories`,
     `/api/departments`, `/images/products`) ötürür. ServiceDesk tərəfində heç nə dəyişmir.
   - `WHATSAPP_API_TOKEN`, `WHATSAPP_GROUP_ID` artıq var. Köhnə token git tarixçəsindədir, ona görə onu
     WaSender panelində yeniləmək tövsiyə olunur.
   - `RABBITMQ_USER` və `RABBITMQ_PASSWORD` artıq istifadə olunmur. Qalsalar, zərəri yoxdur.
   - `APP_IMAGE` və `DB_NAME` dəyərlərini skript özü yazır.
2. **Gecə backup-ı.** `scripts/postgres_backup.sh` hazırda köhnə bazaları saxlayır. Keçiddən sonra
   `inventory` bazası da (və ya yalnız o) saxlanılmalıdır. Keçiddən əvvəl skripti yoxlayın.
3. **Image və fayllar.** GitHub-da *Actions → Prepare cut-over → Run workflow* açın (branch: `master`).
   Workflow serverdə `next/` qovluğunu yaradır (yeni compose, `deploy/`, image adı) və image-i çəkir.
   Bu zaman işləyən sistemə toxunulmur.
   - Ən son commit üçün *Actions → CD* yaşıl olmalıdır. Image-i o yaradır.
   - Qırmızıdırsa, bir az gözləyib yenidən işə salın.
4. **Yoxlama.** Bu addım heç nəyi dəyişmir, keçiddən bir gün əvvəl edin:
   ```bash
   cd /opt/inventory166
   bash next/deploy/cutover.sh check
   ```
   Sonda `Preflight OK` görünməlidir. Skript nəyi yoxlayır:
   - image serverdədir;
   - `.env`-də lazımi açarlar var;
   - `COMPOSE_PROJECT_NAME` işləyən Postgres-in layihəsinə uyğundur (əks halda yeni stack boş baza ilə açılardı);
   - 5 köhnə baza var, `inventory` bazası isə hələ yoxdur;
   - diskdə yer var.

## 1. Keçid (dayanma: bir neçə dəqiqə)

İstifadəçilərə əvvəlcədən xəbər verin, sonra:

```bash
cd /opt/inventory166
bash next/deploy/cutover.sh
```

Skript bu addımları icra edir:
1. Köhnə tətbiq konteynerlərini dayandırır. Postgres və Seq işləməyə davam edir.
2. Bütün bazaların tam nüsxəsini götürür: `/root/pre-cutover-<vaxt>.sql.gz`. 5 bazanın hamısının nüsxədə olduğunu yoxlayır.
3. `inventory` bazasını yaradır.
4. `docker-compose.yml` faylını yenisi ilə əvəz edir. Köhnəsi `docker-compose.old.yml` kimi saxlanılır.
5. Yeni tətbiqi bir dəfə işə salır ki, schema-lar yaransın, sonra onu dayandırır.
6. Datanı köçürür: `deploy/migrate-data.sh`.
   - Hər cədvəl üçün köhnə və yeni sətir sayı göstərilir və hamısı `OK` olmalıdır.
   - Uyğunsuzluq olarsa, köçürmə geri alınır və skript dayanır.
7. Yeni sistemi başladır. `/health` və giriş səhifəsinin (200) cavab verməsini yoxlayır.

Köhnə konteynerlər silinmir, yalnız dayandırılır. Onlar *Təmizlik* addımına qədər qalır.

## 2. Yoxlama siyahısı

- `https://inventory166.az` açılır və mövcud istifadəçilərlə giriş işləyir.
- Məhsul, route, kateqoriya və departament sayları köhnə sistemlə eynidir.
- Məhsul və route şəkilləri görünür.
- Bildiriş zəngi real vaxtda yenilənir: ikinci brauzerdə bir əməliyyat edib yoxlayın.
- Gözləyən approval sorğusu varsa, birini təsdiqləyin və nəticəni yoxlayın.
- ServiceDesk inteqrasiyası işləyir. Serverdə yoxlayın: açarla 200, açarsız 401 gözlənilir.
  ```bash
  K=$(grep ^SERVICEDESK_API_KEY= .env | cut -d= -f2- | tr -d "'")
  curl -s -o /dev/null -w '%{http_code}\n' -H "X-Api-Key: $K" 'http://localhost:5001/api/products?pageSize=1'
  curl -s -o /dev/null -w '%{http_code}\n' 'http://localhost:5001/api/products?pageSize=1'
  ```
- Seq (`http://<server>:5342`) `ApplicationName = InventoryManagement` qeydlərini göstərir.

## 3. CD-ni aktiv edin

GitHub-da *Settings → Secrets and variables → Actions → Variables* bölməsində:
- `DEPLOY_DIR` = `/opt/inventory166` (yoxdursa əlavə edin);
- `CD_ENABLED` = `true`.

Bundan sonra `master`-ə hər push avtomatik deploy olunur (bax: `deploy/CD.md`).

## 4. (İxtiyari) Operator rolunun icazələri

Köhnə seed-dəki sürüşmə səbəbindən Operator rolu:
- məhsulları **approval-sız** yarada və yeniləyə bilir;
- amma silmə sorğusu göndərə bilmir.

Niyyət olunan vəziyyətə keçmək üçün:

```bash
cd /opt/inventory166
docker exec -i inventory_postgres psql -U "$(grep ^DB_USER= .env | cut -d= -f2-)" -d inventory \
  < deploy/sql/fix-operator-permissions.sql
```

Dəyişiklik istifadəçilərin sessiyasına ən geci 5 dəqiqə ərzində tətbiq olunur.

## Geri qaytarma

Yeni sistemdə həll olunmayan problem olarsa:

```bash
cd /opt/inventory166
bash deploy/cutover.sh rollback
```

- Yeni konteynerlər dayanır, köhnə compose faylı yerinə qayıdır və köhnə konteynerlər köhnə bazalarla
  yenidən başlayır.
- Keçiddən sonra yeni sistemdə daxil edilən data köhnə bazalarda **yoxdur**. Uzun müddətdən sonra geri
  qayıtmaq lazım olarsa, həmin datanı əl ilə köçürmək lazım gələcək.
- Yenidən cəhd etmək üçün əvvəlcə `inventory` bazasını silin, sonra *0.3*-dən davam edin:
  ```bash
  docker exec inventory_postgres dropdb -U "$(grep ^DB_USER= .env | cut -d= -f2-)" inventory
  ```

## Təmizlik (1–2 həftə problemsiz işlədikdən sonra)

Köhnə konteynerlər, bazalar və fayllar:

```bash
cd /opt/inventory166
docker rm inventory_web inventory_api_gateway inventory_identity_service inventory_product_service \
  inventory_route_service inventory_approval_service inventory_notification_service inventory_rabbitmq
for db in identity_service product_service route_service approval_service notification_service; do
  docker exec inventory_postgres dropdb -U "$(grep ^DB_USER= .env | cut -d= -f2-)" "$db"
done
docker volume rm inventory166_rabbitmq_data
rm -rf src nginx.conf docker-compose.old.yml deploy.old-* next
docker image prune -a   # köhnə servis image-ləri (işləyən konteynerə aid olmayanlar silinir)
```

`/root/pre-cutover-*.sql.gz` nüsxəsini ayrıca bir yerdə saxlayın. Bu, köhnə sistemin son tam vəziyyətidir.
