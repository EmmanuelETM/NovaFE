import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,

  {
    name: "app/typescript-estricto",
    rules: {
      // Nada de `any`. Los tipos de la API vienen generados del OpenAPI, asi que un
      // `any` no es un atajo: es tirar a la basura la unica garantia de que el nombre
      // del campo existe. Cuando de verdad no se sabe el tipo, `unknown` obliga a
      // estrecharlo antes de usarlo, que es justo lo que se quiere.
      "@typescript-eslint/no-explicit-any": "error",

      // El `!` a veces es correcto —despues de comprobar una condicion que el
      // compilador no puede seguir— pero cada uno merece una mirada. Aviso, no error.
      "@typescript-eslint/no-non-null-assertion": "warn",

      // Importar tipos como tipos: el bundler los elimina y no arrastra el modulo.
      "@typescript-eslint/consistent-type-imports": [
        "error",
        { prefer: "type-imports", fixStyle: "inline-type-imports" },
      ],

      // Prefijo `_` para lo que se ignora a proposito.
      "@typescript-eslint/no-unused-vars": [
        "error",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
        },
      ],
    },
  },

  {
    // El schema viene generado: no se le exige nada.
    name: "app/generado",
    files: ["lib/api/schema.d.ts"],
    rules: {
      "@typescript-eslint/no-explicit-any": "off",
      "@typescript-eslint/consistent-type-imports": "off",
      // openapi-typescript emite interfaces vacias para las secciones que el documento
      // no declara, y el marcador de posicion del scaffold hace lo mismo.
      "@typescript-eslint/no-empty-object-type": "off",
    },
  },

  {
    // Lo que escribe la CLI de shadcn. No se edita a mano —el siguiente `shadcn add` lo
    // reescribiria— asi que tampoco se le puede pedir que cumpla nuestras reglas.
    //
    // El caso concreto que lo motivo: `hooks/use-mobile.ts` llama a `setState` dentro de
    // un efecto, y el lint del React Compiler lo rechaza. Ahi es correcto: el ancho de la
    // ventana no existe en el servidor, asi que la primera medida solo puede tomarse
    // despues de montar. Lo idiomatico en React 19 seria `useSyncExternalStore`, pero
    // reescribirlo aqui se perderia en la siguiente generacion.
    name: "app/shadcn",
    files: ["components/ui/**", "hooks/use-mobile.ts"],
    rules: {
      "react-hooks/set-state-in-effect": "off",
    },
  },

  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
