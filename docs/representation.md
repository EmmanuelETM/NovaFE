# Representación Impresa (Módulo 9)

**Estado: implementado (formatos Carta y POS 80 mm).**
`GET /api/v1/ecf/{id}/representation` devuelve el PDF de la Representación Impresa
de un comprobante emitido, con el timbre QR y el código de seguridad visibles
(Decreto 254-06, Norma 06-2018, RF-09.x).

El **envío por correo** al comprador (RF-09.7) es un slice posterior.

---

## De dónde salen los datos

La RI se arma del **`<ECF>` firmado que ya se guarda** (`issued_ecf.ecf_xml`) —
fuente única, sin snapshot paralelo — más el `security_code`, la `qr_url` y el
estado DGII de la misma fila. Para un tipo 32 que a la DGII fue como **RFCE**, la
RI igual se pinta del `<ECF>` completo: el resumen es solo para la transmisión.

Piezas:

| Interfaz (Application) | Impl (Infrastructure) | Rol |
|---|---|---|
| `IEcfRepresentationReader` | `EcfXmlRepresentationReader` | `<ECF>` → `RepresentationModel` (`XDocument`, ignora `<Signature>`, tolerante a lo que falte). Mapea códigos de la DGII a texto en español. |
| `IRepresentationRenderer` | `QuestPdfRepresentationRenderer` | `RepresentationModel` + `RepresentationLayout` → `byte[]` PDF. |
| `GetEcfRepresentationUseCase` | — | Resuelve el comprobante, lee el XML, arma el modelo con el timbre + estado, renderiza. Solo lectura. |

## El endpoint

```
GET /api/v1/ecf/{id}/representation?layout=letter&download=false
X-Tenant-Id: <guid>
```

- `layout` — `letter` (por defecto) o `pos` (rollo térmico de 80 mm).
- `download=true` → `Content-Disposition: attachment` (descarga); sin él, `inline`
  (se abre en el navegador). El nombre de archivo es `{e-NCF}.pdf` (`{e-NCF}-pos.pdf`
  para el formato POS).
- `404` si el comprobante no existe para el tenant.
- `EcfDto.links.representation` apunta a este endpoint.

## Diseño

QuestPDF (sin navegador; Skia nativo embebido). La fuente **Geist** (OFL) va
vendorizada y embebida como recurso en `src/Infrastructure/Representation/Fonts/`;
`RepresentationFonts` la registra y fija la licencia Community.

Tokens en `RepresentationTheme`: paleta corta (tinta casi negra, grises, hairline,
un acento), escala tipográfica, unidad de espaciado. La jerarquía es por peso y
tamaño, no por cajas. Geist Mono para el e-NCF, los códigos y los montos (cifras
tabulares). `RepresentationText` centraliza el formato (montos, cantidades,
fechas, sello DGII, dominio de verificación) que comparten los dos layouts, más
la atribución del pie (`Con tecnología de Nemus Systems` — el proveedor
tecnológico, no el emisor; en gris tenue, subordinada a los datos fiscales).

**Carta** (`LetterRepresentationDocument`): **cabecera de dos columnas** — emisor
a la izquierda (nombre, RNC, dirección, teléfonos, correo, actividad), identidad
fiscal del comprobante a la derecha como lista `etiqueta → valor` (`e-NCF`,
`e-NCF modificado` para NC/ND, `Válida hasta`, `N° interno`, `Fecha de emisión`,
`Modificación`). Debajo, una fila con el **comprador** a la izquierda y las
**condiciones** a la derecha (condición y fecha límite de pago, tipo de ingreso,
moneda si no es DOP). Luego la tabla de líneas con separadores hairline — la
columna **Importe** es el neto **más el ITBIS** de esa línea (`GrossAmount`) —, el
panel de totales a la derecha, y el timbre (QR + código de seguridad + sello de
estado DGII) tras los totales. Cabecera y encabezado de columnas se repiten al
paginar; "Página X de Y" en el pie.

**POS 80 mm** (`PosRepresentationDocument`): rollo térmico, **una sola columna** y
**página continua** (la altura crece con el contenido, sin paginación). Mismos
datos y misma paleta, tipografía un punto más grande (imprime a ~203 dpi) y montos
en Geist Mono alineados a la derecha: emisor centrado, identidad fiscal como pares
`etiqueta valor`, comprador, líneas (`n descripción` + `cant × precio … importe`),
totales, formas de pago, y el timbre centrado (QR ~46 mm + código de seguridad +
sello DGII). El nombre de archivo lleva el sufijo `-pos`.

**Para ver el diseño:** el test `RepresentationRendererTests` vuelca PDF + PNG de
muestra a `samples/representation/` (gitignored):

```bash
dotnet test tests/UnitTests/NovaFE.UnitTests.csproj --filter "FullyQualifiedName~RepresentationRendererTests"
```

## Notas de operación

- **Licencia QuestPDF**: Community, gratis para organizaciones con **< US$1M** de
  ingresos anuales. Revisar cuando cambie. La alternativa MIT sería
  `PdfSharpCore`/`MigraDoc`, con menos techo de diseño.
- **Docker**: la imagen base (`src/Service/Dockerfile`) instala `libfontconfig1`,
  que el Skia de QuestPDF necesita en Linux.

## Fuera de alcance (por ahora)

- **Correo al comprador** con `<CorreoComprador>` (RF-09.7) — necesita infra de
  correo; su propio slice.
- **Leyenda de contingencia** verbatim (RF-09.5) — es Módulo 11; el
  `RepresentationModel` ya lleva un `ContingencyNotice?` opcional que el renderer
  imprime si está.
- Catálogos de **Tabla III** (municipios/provincias — hoy no se muestran, son
  códigos) y de **unidades de medida**; accent color / logo por tenant (tema
  neutro fijo); conformidad PDF/A.
