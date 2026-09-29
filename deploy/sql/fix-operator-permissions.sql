-- OPTIONAL. Aligns the Operator role with what its original seed intended.
--
-- An off-by-one in the old seed gave Operators product.create.direct and product.update.direct
-- (they create/update products WITHOUT approval) and no product.create / product.update /
-- product.delete request permissions. After this script Operators request approval for product
-- create/update/delete; route permissions are unchanged. Individual users' direct grants
-- (UserPermissions) are not touched.
--
-- Run against the monolith database after the data migration:
--   docker exec -i inventory_postgres psql -U "$DB_USER" -d inventory < deploy/sql/fix-operator-permissions.sql

BEGIN;

DELETE FROM identity."RolePermissions" rp
USING identity."AspNetRoles" r, identity."Permissions" p
WHERE rp."RoleId" = r."Id" AND rp."PermissionId" = p."Id"
  AND r."NormalizedName" = 'OPERATOR'
  AND p."Name" IN ('product.create.direct', 'product.update.direct');

INSERT INTO identity."RolePermissions" ("RoleId", "PermissionId")
SELECT r."Id", p."Id"
FROM identity."AspNetRoles" r
CROSS JOIN identity."Permissions" p
WHERE r."NormalizedName" = 'OPERATOR'
  AND p."Name" IN ('product.create', 'product.update', 'product.delete')
ON CONFLICT DO NOTHING;

COMMIT;
