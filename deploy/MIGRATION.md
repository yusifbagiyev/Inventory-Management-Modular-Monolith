# Mikroservislərdən modular monolitə keçid (production)

Bu təlimat köhnə 7 konteynerli sistemi (5 servis + Ocelot gateway + ayrıca Web, RabbitMQ) yeni tək
`app` konteynerinə keçirir. Data 5 ayrı bazadan (`identity_service`, `product_service`,
`route_service`, `approval_service`, `notification_service`) bir `inventory` bazasına, hər modul
üçün ayrıca schema-ya (`identity`, `product`, `route`, `approval`, `notification`) köçürülür.

**Köhnə bazalara yalnız oxumaq üçün toxunulur.** Geri qayıtmaq üçün köhnə stack-i yenidən
qaldırmaq kifayətdir (bax: *Geri qaytarma*).

Gözlənilən dayanma müddəti: ~15–30 dəqiqə (əsasən image build).

## 0. Hazırlıq (dayanmadan əvvəl)

1. Serverdə repo qovluğunda bu branch-ı çəkin (hələ işə salmayın).
2. `.env` faylını `.env.example` əsasında yeniləyin:
   - `DB_NAME=inventory` (yeni baza)
   - `SERVICEDESK_API_KEY` — **yeni** açar yaradın (köhnə açar git tarixçəsindədir) və ServiceDesk tərəfində də dəyişin.
   - `WHATSAPP_API_TOKEN`, `WHATSAPP_GROUP_ID` — köhnə token git tarixçəsində olduğu üçün WaSender panelində yeniləyin.
   - `RABBITMQ_*` açarları artıq lazım deyil.
3. `ssl/inventory166.crt` və `ssl/inventory166.key` repo kökündə `ssl/` qovluğunda olmalıdır (nginx onları oradan oxuyur).
4. **Compose layihə adını yoxlayın.** Yeni stack köhnə `postgres_data` volume-unu görməlidir:
   ```bash
   docker volume ls | grep postgres_data
   ```
   Çıxan ad `<layihə>_postgres_data` formasındadır. `<layihə>` bu qovluğun adından fərqlidirsə,
   `.env`-ə `COMPOSE_PROJECT_NAME=<layihə>` əlavə edin. Əks halda yeni stack **boş** baza ilə başlayacaq.

## 1. Köhnə tətbiq konteynerlərini dayandırın (Postgres işləməyə davam edir)

```bash
docker stop inventory_nginx inventory_web inventory_api_gateway \
  inventory_identity_service inventory_product_service inventory_route_service \
  inventory_approval_service inventory_notification_service inventory_rabbitmq
```

## 2. Tam ehtiyat nüsxə

```bash
docker exec inventory_postgres pg_dumpall -U "$DB_USER" > backup-before-monolith-$(date +%F).sql
ls -lh backup-before-monolith-*.sql   # boş olmadığını yoxlayın
```

## 3. Yeni bazanı və qovluqları hazırlayın

```bash
docker exec inventory_postgres createdb -U "$DB_USER" inventory

# Konteyner uid 1654 (non-root) ilə işləyir; açar və şəkil qovluqlarına yaza bilməlidir.
mkdir -p storage/keys storage/images/products storage/images/routes
sudo chown -R 1654:1654 storage/keys storage/images
```

## 4. Yeni tətbiqi bir dəfə işə salın (schema-lar yaradılır), sonra dayandırın

```bash
docker compose up -d --build app
docker compose logs -f app      # "InventoryManagement configured successfully" görünənə qədər gözləyin
docker compose stop app
```

İlk başlanğıcda `__EFMigrationsHistory` üçün bir neçə `Failed executing DbCommand` qeydi normaldır
(EF Core cədvəlin mövcudluğunu belə yoxlayır).

## 5. Datanı köçürün

```bash
PG_CONTAINER=inventory_postgres DB_USER="$DB_USER" TARGET_DB=inventory ./deploy/migrate-data.sh
```

Skript hər cədvəl üçün köhnə və yeni sətir sayını göstərir. Hamısı `OK` olmalıdır, əks halda
skript xəta ilə dayanır və tətbiqi başlatmamalısınız.

## 6. Yeni sistemi başladın

```bash
docker compose up -d
docker compose ps
```

Yoxlama siyahısı:
- `https://inventory166.az` açılır, mövcud istifadəçilərlə giriş işləyir.
- Məhsul, route, kateqoriya və departament sayları köhnə sistemlə eynidir.
- Məhsul və route şəkilləri görünür.
- Bildiriş zəngi real vaxtda yenilənir: ikinci brauzerdə bir əməliyyat edib yoxlayın.
- Gözləyən approval sorğusu varsa, birini təsdiqləyin və nəticəni yoxlayın.
- Seq (`http://<server>:5342`) `ApplicationName = InventoryManagement` qeydlərini göstərir.

## 7. (İxtiyari) Operator rolunun icazələri

Köhnə seed-də sürüşmə səbəbindən Operator rolu məhsulları **approval-sız** yaradıb yeniləyə bilir,
amma silmə sorğusu göndərə bilmir. Niyyət olunan vəziyyətə keçmək üçün:

```bash
docker exec -i inventory_postgres psql -U "$DB_USER" -d inventory < deploy/sql/fix-operator-permissions.sql
```

Dəyişiklik istifadəçilərin sessiyasına ən geci 5 dəqiqə ərzində tətbiq olunur.

## Geri qaytarma

Yeni stack-dəki problem həll olunmursa:

```bash
docker compose down            # yalnız yeni konteynerlər; volume-lar qalır
git checkout <əvvəlki versiya>  # köhnə compose faylı
# köhnə konteynerləri əvvəlki kimi başladın
```

Köhnə bazalar keçid zamanı dəyişdirilmir. Keçiddən sonra yeni sistemdə daxil edilən data köhnə
bazalarda **yoxdur**. Uzun müddətdən sonra geri qayıtmaq lazım olarsa, onu əl ilə köçürmək lazım gələcək.

## Təmizlik (1–2 həftə problemsiz işlədikdən sonra)

```bash
for db in identity_service product_service route_service approval_service notification_service; do
  docker exec inventory_postgres dropdb -U "$DB_USER" "$db"
done
docker volume rm <layihə>_rabbitmq_data
docker image prune
```
