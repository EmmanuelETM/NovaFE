-- ============================================================================
--  Rol de aplicación restringido para NovaFE (runtime)
-- ============================================================================
--  Para que Row-Level Security realmente aísle por tenant, la app NO debe
--  conectarse como superusuario, ni como dueño de las tablas, ni con BYPASSRLS.
--  (Algunos Postgres gestionados dan BYPASSRLS al rol por defecto aunque no sea
--  superusuario — Neon lo hace con `neondb_owner`.)
--
--  Las migraciones las corre el rol dueño; el runtime usa `novafe_app`, creado
--  acá. Contexto: docs/multi-tenancy.md §"El rol de aplicación en producción".
--
--  Uso:
--    1. Conectate a la base como el rol dueño (el de Neon por defecto, o el
--       editor SQL de la consola de Neon).
--    2. EDITÁ las dos líneas de PSQL SET de abajo — o, si tu cliente no soporta
--       `\set` (p. ej. el editor de Neon), borralas y reemplazá :'app_password'
--       por 'tu-contraseña' y :"dbname" por el nombre real de la base.
--    3. Corré este archivo entero.
--    4. El connection string del runtime usa novafe_app, NO el dueño.
--
--  Idempotente: se puede correr varias veces.
-- ============================================================================

\set app_password 'CAMBIAR_ESTA_CONTRASENA'
\set dbname 'neondb'

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'novafe_app') THEN
        EXECUTE format('CREATE ROLE novafe_app LOGIN PASSWORD %L', :'app_password');
    ELSE
        EXECUTE format('ALTER ROLE novafe_app WITH LOGIN PASSWORD %L', :'app_password');
    END IF;
END
$$;

-- Sin privilegios de superusuario ni BYPASSRLS (defaults, pero explícito).
ALTER ROLE novafe_app NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;

-- Acceso a datos, nunca DDL.
GRANT CONNECT ON DATABASE :"dbname" TO novafe_app;
GRANT USAGE ON SCHEMA public TO novafe_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO novafe_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO novafe_app;

-- Que las tablas/secuencias futuras (nuevas migraciones) hereden los mismos
-- permisos automáticamente. Se aplica a lo que cree el rol que corre ESTE script
-- (el dueño), así que corré esto DESPUÉS de cada release que agregue tablas — o
-- volvé a correr los dos GRANT … ON ALL … de arriba.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO novafe_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO novafe_app;

-- Nota: FORCE ROW LEVEL SECURITY (lo pone cada migración de tabla ITenantOwned
-- vía RowLevelSecurity.Enable) hace que ni el dueño se salte la política. Una
-- tarea de mantenimiento que necesite ver todo debe fijar app.tenant_id o usar
-- un rol con BYPASSRLS explícito y auditado.
--
-- Cubierto por RowLevelSecurityTests (integración): valida este mismo modelo de
-- grants + NOBYPASSRLS contra las políticas reales.
