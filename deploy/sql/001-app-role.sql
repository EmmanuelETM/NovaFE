-- ============================================================================
--  Rol de aplicación restringido para NovaFE (runtime)
-- ============================================================================
--  Para que Row-Level Security realmente aísle por tenant, la app NO debe
--  conectarse como superusuario, ni como dueño de las tablas, ni con BYPASSRLS.
--  Las migraciones las corre el rol dueño (el rol por defecto de Neon / de la
--  base); el runtime usa `novafe_app`, creado acá.
--
--  Contexto: docs/multi-tenancy.md §"El rol de aplicación en producción".
--
--  Uso:
--    1. Conectate a la base como el rol dueño (el que trae Neon por defecto).
--    2. Editá la contraseña de abajo (o usá \set antes de correr).
--    3. psql "<connection string del dueño>" -f deploy/sql/001-app-role.sql
--    4. El connection string de la Container App usa novafe_app, NO el dueño.
--
--  Idempotente: se puede correr varias veces.
-- ============================================================================

\set app_password `echo "${NOVAFE_APP_PASSWORD:-CAMBIAR_ESTA_CONTRASENA}"`
\set dbname `echo "${NOVAFE_DB_NAME:-novafe}"`

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

-- Acceso a datos, nunca DDL. Correr conectado a la base objetivo (:dbname).
GRANT CONNECT ON DATABASE :"dbname" TO novafe_app;
GRANT USAGE ON SCHEMA public TO novafe_app;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO novafe_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO novafe_app;

-- Que las tablas/secuencias futuras (nuevas migraciones) hereden los mismos
-- permisos automáticamente. Se aplica a lo que cree el rol que corre ESTE script.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO novafe_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO novafe_app;

-- Nota: FORCE ROW LEVEL SECURITY (lo pone cada migración de tabla ITenantOwned
-- vía RowLevelSecurity.Enable) hace que ni el dueño se salte la política. Una
-- tarea de mantenimiento que necesite ver todo debe fijar app.tenant_id o usar
-- un rol con BYPASSRLS explícito y auditado.
