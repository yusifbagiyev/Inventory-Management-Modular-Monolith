# CD: master-ə merge → production

`master`-ə hər merge-dən sonra CI (`ci.yml`) keçərsə, `cd.yml`:

1. GitHub-da Docker image-i build edib `ghcr.io/<owner>/inventory-app:sha-<commit>` (və `:latest`) kimi push edir.
2. Production serverdəki **self-hosted runner** həmin image-i `deploy/deploy.sh` ilə yayır:
   - bazanın ehtiyat nüsxəsi (`backups/pre-deploy-*.dump`, son 14-ü saxlanılır);
   - yalnız `app` konteyneri yeni image ilə yenidən yaradılır;
   - `/health` (DB əlaqəsi daxil) keçənə qədər gözlənilir (4 dəq);
   - keçməsə, əvvəlki image avtomatik geri qaytarılır və workflow qırmızı olur;
   - uğurlu olsa, image `.env`-də `APP_IMAGE` kimi yazılır. Adi `docker compose up -d` də həmin versiyanı işlədir.

Runner GitHub-a özü qoşulur (yalnız çıxan HTTPS). Serverə kənardan port açmaq lazım deyil.

> **Əvvəlcə keçid.** Serverdə hələ köhnə mikroservislər işləyirsə, CD-ni aktiv etməyin. Əvvəl
> `deploy/MIGRATION.md`-i tam icra edin. CD `CD_ENABLED` dəyişəni `true` olana qədər heç nə etmir.

## Bir dəfəlik quraşdırma (serverdə)

Aşağıda `/opt/inventory` MIGRATION.md-də istifadə etdiyiniz qovluqdur (`.env`, `ssl/`, `storage/` oradadır).
Fərqlidirsə, öz yolunuzu yazın.

1. **Runner istifadəçisi.** `docker` qrupu root-a bərabər hüquq verir. Buna görə ayrıca istifadəçi yaradın:
   ```bash
   sudo useradd -m -s /bin/bash github-runner
   sudo usermod -aG docker github-runner
   sudo chown -R github-runner: /opt/inventory
   ```
   Konteyner `storage/keys` və `storage/images`-ə uid 1654 ilə yazır. Onların sahibini MIGRATION.md-dəki kimi saxlayın:
   ```bash
   sudo chown -R 1654:1654 /opt/inventory/storage
   ```

2. **Runner-i quraşdırın.**
   - GitHub-da: repo → *Settings → Actions → Runners → New self-hosted runner* → *Linux x64*.
   - Orada göstərilən əmrləri `github-runner` istifadəçisi ilə icra edin.
   - `config.sh`-a label əlavə edin:
     ```bash
     ./config.sh --url https://github.com/<owner>/<repo> --token <token> --labels inventory-prod --unattended
     sudo ./svc.sh install github-runner
     sudo ./svc.sh start
     ```
   Runner *Runners* siyahısında `Idle` görünməlidir.

3. **Repo dəyişənləri.** *Settings → Secrets and variables → Actions → Variables* bölməsinə bunları əlavə edin:
   - `DEPLOY_DIR` = `/opt/inventory`
   - `CD_ENABLED` = `true` (yalnız MIGRATION.md bitdikdən sonra)

   Secret lazım deyil. Image-i workflow-un öz `GITHUB_TOKEN`-i ilə push və pull edir.

4. **İlk deploy.** *Actions → CD → Run workflow* (branch: `master`, `image_tag` boş).

## Gündəlik iş

- PR → CI yaşıl → merge. Deploy özü başlayır. Nəticəyə *Actions → CD*-dən baxın.
- Deploy tarixçəsi serverdə `/opt/inventory/deploy-history.log` faylındadır (vaxt + image).
- CD-ni müvəqqəti dayandırmaq üçün `CD_ENABLED` dəyərini `false` edin.

## Serverə məxsus dəyişikliklər

Hər deploy `docker-compose.yml` və `deploy/` qovluğunu repodan yenidən köçürür. Serverdə edilən
dəyişikliklər (port, əlavə volume və s.) `docker-compose.override.yml` faylına yazılmalıdır. Compose
onu avtomatik oxuyur, CD isə ona toxunmur. Məsələn, Postgres portunu bağlamaq üçün:
```yaml
services:
  postgres:
    ports: !reset []
```
Sirlər həmişə `.env`-də qalır.

## Geri qaytarma

- **Avtomatik:** yeni versiya sağlam olmasa, `deploy.sh` əvvəlkini özü qaytarır.
- **Əl ilə, GitHub-dan:**
  - *Actions → CD → Run workflow* → `image_tag` sahəsinə əvvəlki tag-i yazın (`sha-<commit>`).
  - Tag-i `deploy-history.log` faylından və ya repo-nun *Packages* bölməsindən götürmək olar.
- **Əl ilə, serverdən (GitHub olmadan):**
  ```bash
  cd /opt/inventory
  docker login ghcr.io   # image private-dırsa; read:packages icazəli token ilə
  DEPLOY_DIR=/opt/inventory deploy/deploy.sh ghcr.io/<owner>/inventory-app:sha-<commit>
  ```

Image-i geri qaytarmaq bazanı geri qaytarmır. Migration-lar startda işləyir. Adətən köhnə versiya
yeni sxemlə də işləyir (migration-lar əlavə xarakterlidir). Problem migration-dadırsa, bazanı
deploy-dan əvvəlki nüsxədən bərpa edin:
```bash
cd /opt/inventory
docker compose stop app
docker exec -i inventory_postgres sh -c 'pg_restore -U "$POSTGRES_USER" -d inventory --clean --if-exists' \
  < backups/pre-deploy-YYYYMMDD-HHMMSS.dump
DEPLOY_DIR=/opt/inventory deploy/deploy.sh ghcr.io/<owner>/inventory-app:sha-<əvvəlki commit>
```
Bərpa həmin nüsxədən sonra daxil edilmiş datanı silir. Bunu yalnız zəruri olanda edin.

## Təhlükəsizlik

- Runner yalnız bu **private** repoya bağlıdır. Yalnız `inventory-prod` label-li job-lar (CD-nin
  deploy addımı) onda işləyir. CI və PR-lar GitHub-un öz serverlərində işləyir. Repo public
  edilərsə, runner-i əvvəlcə silin.
- `backups/` qovluğu (700) bazanın tam nüsxəsini saxlayır. Server backup-larına daxil edin, amma
  kənara açmayın.
