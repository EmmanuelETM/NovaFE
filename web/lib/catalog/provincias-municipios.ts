/**
 * Catálogo de provincias, municipios y distritos municipales de la DGII
 * (Tabla III — Codificación Provincias y Municipios), tal como los define
 * `ProvinciaMunicipioType` en el XSD oficial `e-CF 31 v.1.0.xsd`
 * (`<Municipio>`/`<Provincia>` del e-CF validan contra este mismo enum).
 * Fuente original citada por la DGII: Oficina Nacional de Estadística (ONE),
 * Departamento de Cartografía — actualizada al 30 de junio de 2014.
 *
 * **Generado, no editar a mano.** El código de un municipio no siempre sigue
 * el patrón `PPMM00` (p. ej. `030001` "MUNICIPIO NEIBA" en vez de
 * `030100`) — la jerarquía se armó por orden de aparición en el XSD, no por
 * prefijo numérico. 32 provincias (31 + Distrito Nacional) · 155 municipios ·
 * 395 distritos municipales · 582 códigos en total.
 *
 * Para regenerar (si la DGII publica una Tabla III nueva): parsear de nuevo
 * el bloque `<xs:simpleType name="ProvinciaMunicipioType">...</xs:simpleType>`
 * del XSD, agrupando `<xs:enumeration value="CODE"/> <!--NOMBRE-->` por
 * orden de aparición — código terminado en `0000` = provincia; si no, el
 * nombre empieza con "MUNICIPIO " = municipio; si no, distrito municipal del
 * municipio más reciente visto.
 */

export interface DistritoMunicipal {
  code: string;
  name: string;
}

export interface Municipio {
  code: string;
  name: string;
  distritos: readonly DistritoMunicipal[];
}

export interface Provincia {
  code: string;
  name: string;
  municipios: readonly Municipio[];
}

export const PROVINCIAS: readonly Provincia[] = [
  {
    code: "010000",
    name: "DISTRITO NACIONAL",
    municipios: [
      {
        code: "010100",
        name: "MUNICIPIO SANTO DOMINGO DE GUZMÁN",
        distritos: [
          { code: "010101", name: "SANTO DOMINGO DE GUZMÁN (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "020000",
    name: "PROVINCIA AZUA",
    municipios: [
      {
        code: "020100",
        name: "MUNICIPIO AZUA",
        distritos: [
          { code: "020101", name: "AZUA (D. M.)." },
          { code: "020102", name: "BARRO ARRIBA (D. M.)." },
          { code: "020103", name: "LAS BARÍAS-LA ESTANCIA (D. M.)." },
          { code: "020104", name: "LOS JOVILLOS (D. M.)." },
          { code: "020105", name: "PUERTO VIEJO (D. M.)." },
          { code: "020106", name: "BARRERAS (D. M.)." },
          { code: "020107", name: "DOÑA EMMA BALAGUER VIUDA VALLEJO (D. M.)." },
          { code: "020108", name: "CLAVELLINA (D. M.)." },
          { code: "020109", name: "LAS LOMAS (D. M.)." },
        ],
      },
      {
        code: "020200",
        name: "MUNICIPIO LAS CHARCAS",
        distritos: [
          { code: "020201", name: "LAS CHARCAS (D. M.)." },
          { code: "020202", name: "PALMAR DE OCOA (D. M.)." },
        ],
      },
      {
        code: "020300",
        name: "MUNICIPIO LAS YAYAS DE VIAJAMA",
        distritos: [
          { code: "020301", name: "LAS YAYAS DE VIAJAMA (D. M.)." },
          { code: "020302", name: "VILLARPANDO (D. M.)." },
          { code: "020303", name: "HATO NUEVO CORTÉS (D. M.)." },
        ],
      },
      {
        code: "020400",
        name: "MUNICIPIO PADRE LAS CASAS",
        distritos: [
          { code: "020401", name: "PADRE LAS CASAS (D. M.)." },
          { code: "020402", name: "LAS LAGUNAS (D. M.)." },
          { code: "020403", name: "LA SIEMBRA (D. M.)." },
          { code: "020404", name: "MONTE BONITO (D. M.)." },
          { code: "020405", name: "LOS FRÍOS (D. M.)." },
        ],
      },
      {
        code: "020500",
        name: "MUNICIPIO PERALTA",
        distritos: [{ code: "020501", name: "PERALTA (D. M.)." }],
      },
      {
        code: "020600",
        name: "MUNICIPIO SABANA YEGUA",
        distritos: [
          { code: "020601", name: "SABANA YEGUA (D. M.)." },
          { code: "020602", name: "PROYECTO 4 (D. M.)." },
          { code: "020603", name: "GANADERO (D. M.)." },
          { code: "020604", name: "PROYECTO 2-C (D. M.)." },
        ],
      },
      {
        code: "020700",
        name: "MUNICIPIO PUEBLO VIEJO",
        distritos: [
          { code: "020701", name: "PUEBLO VIEJO (D. M.)." },
          { code: "020702", name: "EL ROSARIO (D. M.)." },
        ],
      },
      {
        code: "020800",
        name: "MUNICIPIO TÁBARA ARRIBA",
        distritos: [
          { code: "020801", name: "TÁBARA ARRIBA (D. M.)." },
          { code: "020802", name: "TÁBARA ABAJO (D. M.)." },
          { code: "020803", name: "AMIAMA GÓMEZ (D. M.)." },
          { code: "020804", name: "LOS TOROS (D. M.)." },
        ],
      },
      {
        code: "020900",
        name: "MUNICIPIO GUAYABAL",
        distritos: [{ code: "020901", name: "GUAYABAL (D. M.)." }],
      },
      {
        code: "021000",
        name: "MUNICIPIO ESTEBANÍA",
        distritos: [{ code: "021001", name: "ESTEBANÍA (D. M.)." }],
      },
    ],
  },
  {
    code: "030000",
    name: "PROVINCIA BAHORUCO",
    municipios: [
      {
        code: "030001",
        name: "MUNICIPIO NEIBA",
        distritos: [
          { code: "030101", name: "NEIBA (D. M.)." },
          { code: "030102", name: "EL PALMAR  (D. M.)." },
        ],
      },
      {
        code: "030200",
        name: "MUNICIPIO GALVÁN",
        distritos: [
          { code: "030201", name: "GALVÁN (D. M.)." },
          { code: "030202", name: "EL SALADO (D. M.)." },
        ],
      },
      {
        code: "030300",
        name: "MUNICIPIO TAMAYO",
        distritos: [
          { code: "030301", name: "TAMAYO (D. M.)." },
          { code: "030302", name: "UVILLA (D. M.)." },
          { code: "030303", name: "SANTANA (D. M.)." },
          { code: "030304", name: "MONSERRATE (MONTSERRAT) (D. M.)." },
          { code: "030305", name: "CABEZA DE TORO (D. M.)." },
          { code: "030306", name: "MENA (D. M.)." },
          { code: "030307", name: "SANTA BÁRBARA EL 6 (D. M.)." },
        ],
      },
      {
        code: "030400",
        name: "MUNICIPIO VILLA JARAGUA",
        distritos: [{ code: "030401", name: "VILLA JARAGUA (D. M.)." }],
      },
      {
        code: "030500",
        name: "MUNICIPIO LOS RÍOS",
        distritos: [
          { code: "030501", name: "LOS RÍOS (D. M.)." },
          { code: "030502", name: "LAS CLAVELLINAS (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "040000",
    name: "PROVINCIA BARAHONA",
    municipios: [
      {
        code: "040100",
        name: "MUNICIPIO BARAHONA",
        distritos: [
          { code: "040101", name: "BARAHONA (D. M.)." },
          { code: "040102", name: "EL CACHÓN (D. M.)." },
          { code: "040103", name: "LA GUÁZARA (D. M.)." },
          { code: "040104", name: "VILLA CENTRAL (D. M.)." },
        ],
      },
      {
        code: "040200",
        name: "MUNICIPIO CABRAL",
        distritos: [{ code: "040201", name: "CABRAL (D. M.)." }],
      },
      {
        code: "040300",
        name: "MUNICIPIO ENRIQUILLO",
        distritos: [
          { code: "040301", name: "ENRIQUILLO (D. M.)." },
          { code: "040302", name: "ARROYO DULCE (D. M.)." },
        ],
      },
      {
        code: "040400",
        name: "MUNICIPIO PARAÍSO",
        distritos: [
          { code: "040401", name: "PARAÍSO (D. M.)." },
          { code: "040402", name: "LOS PATOS (D. M.)." },
        ],
      },
      {
        code: "040500",
        name: "MUNICIPIO VICENTE NOBLE",
        distritos: [
          { code: "040501", name: "VICENTE NOBLE (D. M.)." },
          { code: "040502", name: "CANOA (D. M.)." },
          { code: "040503", name: "QUITA CORAZA (D. M.)." },
          { code: "040504", name: "FONDO NEGRO (D. M.)." },
        ],
      },
      {
        code: "040600",
        name: "MUNICIPIO EL PEÑÓN",
        distritos: [{ code: "040601", name: "EL PEÑÓN (D. M.)." }],
      },
      {
        code: "040700",
        name: "MUNICIPIO LA CIÉNAGA",
        distritos: [
          { code: "040701", name: "LA CIÉNAGA (D. M.)." },
          { code: "040702", name: "BAHORUCO (D. M.)." },
        ],
      },
      {
        code: "040800",
        name: "MUNICIPIO FUNDACIÓN",
        distritos: [
          { code: "040801", name: "FUNDACIÓN (D. M.)." },
          { code: "040802", name: "PESCADERÍA (D. M.)." },
        ],
      },
      {
        code: "040900",
        name: "MUNICIPIO LAS SALINAS",
        distritos: [{ code: "040901", name: "LAS SALINAS (D. M.)." }],
      },
      {
        code: "041000",
        name: "MUNICIPIO POLO",
        distritos: [{ code: "041001", name: "POLO (D. M.)." }],
      },
      {
        code: "041100",
        name: "MUNICIPIO JAQUIMEYES",
        distritos: [
          { code: "041101", name: "JAQUIMEYES (D. M.)." },
          { code: "041102", name: "PALO ALTO (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "050000",
    name: "PROVINCIA DAJABÓN",
    municipios: [
      {
        code: "050100",
        name: "MUNICIPIO DAJABÓN",
        distritos: [
          { code: "050101", name: "DAJABÓN (D. M.)." },
          { code: "050102", name: "CAÑONGO (D. M.)." },
        ],
      },
      {
        code: "050200",
        name: "MUNICIPIO LOMA DE CABRERA",
        distritos: [
          { code: "050201", name: "LOMA DE CABRERA (D. M.)." },
          { code: "050202", name: "CAPOTILLO (D. M.)." },
          { code: "050203", name: "SANTIAGO DE LA CRUZ (D. M.)." },
        ],
      },
      {
        code: "050300",
        name: "MUNICIPIO PARTIDO",
        distritos: [{ code: "050301", name: "PARTIDO (D. M.)." }],
      },
      {
        code: "050400",
        name: "MUNICIPIO RESTAURACIÓN",
        distritos: [{ code: "050401", name: "RESTAURACIÓN (D. M.)." }],
      },
      {
        code: "050500",
        name: "MUNICIPIO EL PINO",
        distritos: [
          { code: "050501", name: "EL PINO (D. M.)." },
          { code: "050502", name: "MANUEL BUENO (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "060000",
    name: "PROVINCIA DUARTE",
    municipios: [
      {
        code: "060100",
        name: "MUNICIPIO SAN FRANCISCO DE MACORÍS",
        distritos: [
          { code: "060101", name: "SAN FRANCISCO DE MACORÍS (D. M.)." },
          { code: "060102", name: "LA PEÑA (D. M.)." },
          { code: "060103", name: "CENOVÍ (D. M.)." },
          { code: "060104", name: "JAYA (D. M.)." },
          {
            code: "060105",
            name: "PRESIDENTE DON ANTONIO GUZMÁN FERNÁNDEZ (D. M.).",
          },
        ],
      },
      {
        code: "060200",
        name: "MUNICIPIO ARENOSO",
        distritos: [
          { code: "060201", name: "ARENOSO (D. M.)." },
          { code: "060202", name: "LAS COLES (D. M.)." },
          { code: "060203", name: "EL AGUACATE (D. M.)." },
        ],
      },
      {
        code: "060300",
        name: "MUNICIPIO CASTILLO",
        distritos: [{ code: "060301", name: "CASTILLO (D. M.)." }],
      },
      {
        code: "060400",
        name: "MUNICIPIO PIMENTEL",
        distritos: [{ code: "060401", name: "PIMENTEL (D. M.)." }],
      },
      {
        code: "060500",
        name: "MUNICIPIO VILLA RIVA",
        distritos: [
          { code: "060501", name: "VILLA RIVA (D. M.)." },
          { code: "060502", name: "AGUA SANTA DEL YUNA  (D. M.)." },
          { code: "060503", name: "CRISTO REY DE GUARAGUAO (D. M.)." },
          { code: "060504", name: "LAS TARANAS (D. M.)." },
          { code: "060505", name: "BARRAQUITO (D. M.)." },
        ],
      },
      {
        code: "060600",
        name: "MUNICIPIO LAS GUÁRANAS",
        distritos: [{ code: "060601", name: "LAS GUÁRANAS (D. M.)." }],
      },
      {
        code: "060700",
        name: "MUNICIPIO EUGENIO MARÍA DE HOSTOS",
        distritos: [
          { code: "060701", name: "EUGENIO MARÍA DE HOSTOS (D. M.)." },
          { code: "060702", name: "SABANA GRANDE (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "070000",
    name: "PROVINCIA ELÍAS PIÑA",
    municipios: [
      {
        code: "070100",
        name: "MUNICIPIO COMENDADOR",
        distritos: [
          { code: "070101", name: "COMENDADOR (D. M.)." },
          { code: "070102", name: "SABANA LARGA (D. M.)." },
          { code: "070103", name: "GUAYABO  (D. M.)." },
        ],
      },
      {
        code: "070200",
        name: "MUNICIPIO BÁNICA",
        distritos: [
          { code: "070201", name: "BÁNICA (D. M.)." },
          { code: "070202", name: "SABANA CRUZ (D. M.)." },
          { code: "070203", name: "SABANA HIGÜERO (D. M.)." },
        ],
      },
      {
        code: "070300",
        name: "MUNICIPIO EL LLANO",
        distritos: [
          { code: "070301", name: "EL LLANO (D. M.)." },
          { code: "070302", name: "GUANITO (D. M.)." },
        ],
      },
      {
        code: "070400",
        name: "MUNICIPIO HONDO VALLE",
        distritos: [
          { code: "070401", name: "HONDO VALLE (D. M.)." },
          { code: "070402", name: "RANCHO DE LA GUARDIA (D. M.)." },
        ],
      },
      {
        code: "070500",
        name: "MUNICIPIO PEDRO SANTANA",
        distritos: [
          { code: "070501", name: "PEDRO SANTANA (D. M.)." },
          { code: "070502", name: "RÍO LIMPIO (D. M.)." },
        ],
      },
      {
        code: "070600",
        name: "MUNICIPIO JUAN SANTIAGO",
        distritos: [{ code: "070601", name: "JUAN SANTIAGO (D. M.)." }],
      },
    ],
  },
  {
    code: "080000",
    name: "PROVINCIA EL SEIBO",
    municipios: [
      {
        code: "080100",
        name: "MUNICIPIO EL SEIBO",
        distritos: [
          { code: "080101", name: "EL SEIBO (D. M.)." },
          { code: "080102", name: "PEDRO SÁNCHEZ (D. M.)." },
          { code: "080103", name: "SAN FRANCISCO-VICENTILLO (D. M.)." },
          { code: "080104", name: "SANTA LUCÍA (D. M.)." },
        ],
      },
      {
        code: "080200",
        name: "MUNICIPIO MICHES",
        distritos: [
          { code: "080201", name: "MICHES (D. M.)." },
          { code: "080202", name: "EL CEDRO (D. M.)." },
          { code: "080203", name: "LA GINA (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "090000",
    name: "PROVINCIA ESPAILLAT",
    municipios: [
      {
        code: "090100",
        name: "MUNICIPIO MOCA",
        distritos: [
          { code: "090101", name: "MOCA (D. M.)." },
          { code: "090102", name: "JOSÉ CONTRERAS (D. M.)." },
          { code: "090103", name: "SAN VÍCTOR (D. M.)." },
          { code: "090104", name: "JUAN LÓPEZ (D. M.)." },
          { code: "090105", name: "LAS LAGUNAS (D. M.)." },
          { code: "090106", name: "CANCA LA REYNA  (D. M.)." },
          { code: "090107", name: "EL HIGÜERITO (D. M.)." },
          { code: "090108", name: "MONTE DE LA JAGUA (D. M.)." },
          { code: "090109", name: "LA ORTEGA (D. M.)." },
        ],
      },
      {
        code: "090200",
        name: "MUNICIPIO CAYETANO GERMOSÉN",
        distritos: [{ code: "090201", name: "CAYETANO GERMOSÉN (D. M.)." }],
      },
      {
        code: "090300",
        name: "MUNICIPIO GASPAR HERNÁNDEZ",
        distritos: [
          { code: "090301", name: "GASPAR HERNÁNDEZ (D. M.)." },
          { code: "090302", name: "JOBA ARRIBA (D. M.)." },
          { code: "090303", name: "VERAGUA (D. M.)." },
          { code: "090304", name: "VILLA MAGANTE (D. M.)." },
        ],
      },
      {
        code: "090400",
        name: "MUNICIPIO JAMAO AL NORTE",
        distritos: [{ code: "090401", name: "JAMAO AL NORTE (D. M.)." }],
      },
    ],
  },
  {
    code: "100000",
    name: "PROVINCIA INDEPENDENCIA",
    municipios: [
      {
        code: "100100",
        name: "MUNICIPIO JIMANÍ",
        distritos: [
          { code: "100101", name: "JIMANÍ (D. M.)." },
          { code: "100102", name: "EL LIMÓN (D. M.)." },
          { code: "100103", name: "BOCA DE CACHÓN (D. M.)." },
        ],
      },
      {
        code: "100200",
        name: "MUNICIPIO DUVERGÉ",
        distritos: [
          { code: "100201", name: "DUVERGÉ (D. M.)." },
          { code: "100202", name: "VENGAN A VER (D. M.)." },
        ],
      },
      {
        code: "100300",
        name: "MUNICIPIO LA DESCUBIERTA",
        distritos: [{ code: "100301", name: "LA DESCUBIERTA (D. M.)." }],
      },
      {
        code: "100400",
        name: "MUNICIPIO POSTRER RÍO",
        distritos: [
          { code: "100401", name: "POSTRER RÍO (D. M.)." },
          { code: "100402", name: "GUAYABAL (D. M.)." },
        ],
      },
      {
        code: "100500",
        name: "MUNICIPIO CRISTÓBAL",
        distritos: [
          { code: "100501", name: "CRISTÓBAL (D. M.)." },
          { code: "100502", name: "BATEY 8 (D. M.)." },
        ],
      },
      {
        code: "100600",
        name: "MUNICIPIO MELLA",
        distritos: [
          { code: "100601", name: "MELLA (D. M.)." },
          { code: "100602", name: "LA COLONIA (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "110000",
    name: "PROVINCIA LA ALTAGRACIA",
    municipios: [
      {
        code: "110100",
        name: "MUNICIPIO HIGÜEY",
        distritos: [
          { code: "110101", name: "HIGÜEY (D. M.)." },
          { code: "110102", name: "LAS LAGUNAS DE NISIBÓN (D. M.)." },
          { code: "110103", name: "LA OTRA BANDA (D. M.)." },
          { code: "110104", name: "VERÓN PUNTA CANA (D. M.) (Incluye Bávaro)" },
        ],
      },
      {
        code: "110200",
        name: "MUNICIPIO SAN RAFAEL DEL YUMA",
        distritos: [
          { code: "110201", name: "SAN RAFAEL DEL YUMA (D. M.)." },
          { code: "110202", name: "BOCA DE YUMA (D. M.)." },
          { code: "110203", name: "BAYAHÍBE (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "120000",
    name: "PROVINCIA LA ROMANA",
    municipios: [
      {
        code: "120100",
        name: "MUNICIPIO LA ROMANA",
        distritos: [
          { code: "120101", name: "LA ROMANA (D. M.)." },
          { code: "120102", name: "CALETA (D. M.)." },
        ],
      },
      {
        code: "120200",
        name: "MUNICIPIO GUAYMATE",
        distritos: [{ code: "120201", name: "GUAYMATE (D. M.)." }],
      },
      {
        code: "120300",
        name: "MUNICIPIO VILLA HERMOSA",
        distritos: [
          { code: "120301", name: "VILLA HERMOSA (D. M.)." },
          { code: "120302", name: "CUMAYASA (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "130000",
    name: "PROVINCIA LA VEGA",
    municipios: [
      {
        code: "130100",
        name: "MUNICIPIO LA VEGA",
        distritos: [
          { code: "130101", name: "LA VEGA (D. M.)." },
          { code: "130102", name: "RÍO VERDE ARRIBA (D. M.)." },
          { code: "130103", name: "EL RANCHITO (D. M.)." },
          { code: "130104", name: "TAVERAS (D. M.)." },
          { code: "130105", name: "DON JUAN RODRÍGUEZ (D.M.)" },
        ],
      },
      {
        code: "130200",
        name: "MUNICIPIO CONSTANZA",
        distritos: [
          { code: "130201", name: "CONSTANZA (D. M.)." },
          { code: "130202", name: "TIREO (D. M.)." },
          { code: "130203", name: "LA SABINA (D. M.)." },
        ],
      },
      {
        code: "130300",
        name: "MUNICIPIO JARABACOA",
        distritos: [
          { code: "130301", name: "JARABACOA (D. M.)." },
          { code: "130302", name: "BUENA VISTA (D. M.)." },
          { code: "130303", name: "MANABAO (D. M.)." },
        ],
      },
      {
        code: "130400",
        name: "MUNICIPIO JIMA ABAJO",
        distritos: [
          { code: "130401", name: "JIMA ABAJO (D. M.)." },
          { code: "130402", name: "RINCÓN (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "140000",
    name: "PROVINCIA MARÍA TRINIDAD SÁNCHEZ",
    municipios: [
      {
        code: "140100",
        name: "MUNICIPIO NAGUA",
        distritos: [
          { code: "140101", name: "NAGUA (D. M.)." },
          { code: "140102", name: "SAN JOSÉ DE MATANZAS (D. M.)." },
          { code: "140103", name: "LAS GORDAS (D. M.)." },
          { code: "140104", name: "ARROYO AL MEDIO (D. M.)." },
        ],
      },
      {
        code: "140200",
        name: "MUNICIPIO CABRERA",
        distritos: [
          { code: "140201", name: "CABRERA (D. M.)." },
          { code: "140202", name: "ARROYO SALADO (D. M.)." },
          { code: "140203", name: "LA ENTRADA (D. M.)." },
        ],
      },
      {
        code: "140300",
        name: "MUNICIPIO EL FACTOR",
        distritos: [
          { code: "140301", name: "EL FACTOR (D. M.)." },
          { code: "140302", name: "EL POZO (D. M.)." },
        ],
      },
      {
        code: "140400",
        name: "MUNICIPIO RÍO SAN JUAN",
        distritos: [{ code: "140401", name: "RÍO SAN JUAN (D. M.)." }],
      },
    ],
  },
  {
    code: "150000",
    name: "PROVINCIA MONTE CRISTI",
    municipios: [
      {
        code: "150100",
        name: "MUNICIPIO MONTE CRISTI",
        distritos: [{ code: "150101", name: "MONTE CRISTI (D. M.)." }],
      },
      {
        code: "150200",
        name: "MUNICIPIO CASTAÑUELAS",
        distritos: [
          { code: "150201", name: "CASTAÑUELAS (D. M.)." },
          { code: "150202", name: "PALO VERDE (D. M.)." },
        ],
      },
      {
        code: "150300",
        name: "MUNICIPIO GUAYUBÍN",
        distritos: [
          { code: "150301", name: "GUAYUBÍN (D. M.)." },
          { code: "150302", name: "VILLA ELISA (D. M.)." },
          { code: "150303", name: "HATILLO PALMA (D. M.)." },
          { code: "150304", name: "CANA CHAPETÓN (D. M.)." },
        ],
      },
      {
        code: "150400",
        name: "MUNICIPIO LAS MATAS DE SANTA CRUZ",
        distritos: [
          { code: "150401", name: "LAS MATAS DE SANTA CRUZ (D. M.)." },
        ],
      },
      {
        code: "150500",
        name: "MUNICIPIO PEPILLO SALCEDO",
        distritos: [
          { code: "150501", name: "PEPILLO SALCEDO (MANZANILLO)" },
          { code: "150502", name: "SANTA MARÍA (D. M.)" },
        ],
      },
      {
        code: "150600",
        name: "MUNICIPIO VILLA VÁSQUEZ",
        distritos: [{ code: "150601", name: "VILLA VÁSQUEZ" }],
      },
    ],
  },
  {
    code: "160000",
    name: "PROVINCIA PEDERNALES",
    municipios: [
      {
        code: "160100",
        name: "MUNICIPIO PEDERNALES",
        distritos: [
          { code: "160101", name: "PEDERNALES" },
          { code: "160102", name: "JOSÉ FRANCISCO PEÑA GÓMEZ (D. M.)." },
        ],
      },
      {
        code: "160200",
        name: "MUNICIPIO OVIEDO",
        distritos: [
          { code: "160201", name: "OVIEDO" },
          { code: "160202", name: "JUANCHO (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "170000",
    name: "PROVINCIA PERAVIA",
    municipios: [
      {
        code: "170100",
        name: "MUNICIPIO BANÍ",
        distritos: [
          { code: "170101", name: "BANÍ (D. M.)." },
          { code: "170102", name: "MATANZAS (D. M.)." },
          { code: "170103", name: "VILLA FUNDACIÓN (D. M.)." },
          { code: "170104", name: "SABANA BUEY (D. M.)." },
          { code: "170105", name: "PAYA (D. M.)." },
          { code: "170106", name: "VILLA SOMBRERO (D. M.)." },
          { code: "170107", name: "EL CARRETÓN (D. M.)." },
          { code: "170108", name: "CATALINA (D. M.)." },
          { code: "170109", name: "EL LIMONAL (D. M.)." },
          { code: "170110", name: "LAS BARÍAS (D. M.)." },
        ],
      },
      {
        code: "170200",
        name: "MUNICIPIO NIZAO",
        distritos: [
          { code: "170201", name: "NIZAO" },
          { code: "170202", name: "PIZARRETE (D. M.)." },
          { code: "170203", name: "SANTANA (D. M.)." },
          { code: "170300", name: "MATANZAS" },
          { code: "170301", name: "MATANZAS" },
        ],
      },
    ],
  },
  {
    code: "180000",
    name: "PROVINCIA PUERTO PLATA",
    municipios: [
      {
        code: "180100",
        name: "MUNICIPIO PUERTO PLATA",
        distritos: [
          { code: "180101", name: "PUERTO PLATA (D. M.)." },
          { code: "180102", name: "YÁSICA ARRIBA (D. M.)." },
          { code: "180103", name: "MAIMÓN (D. M.)." },
        ],
      },
      {
        code: "180200",
        name: "MUNICIPIO ALTAMIRA",
        distritos: [
          { code: "180201", name: "ALTAMIRA" },
          { code: "180202", name: "RÍO GRANDE (D. M.)." },
        ],
      },
      {
        code: "180300",
        name: "MUNICIPIO GUANANICO",
        distritos: [{ code: "180301", name: "GUANANICO" }],
      },
      {
        code: "180400",
        name: "MUNICIPIO IMBERT",
        distritos: [{ code: "180401", name: "IMBERT" }],
      },
      {
        code: "180500",
        name: "MUNICIPIO LOS HIDALGOS",
        distritos: [
          { code: "180501", name: "LOS HIDALGOS" },
          { code: "180502", name: "NAVAS (D. M.)." },
        ],
      },
      {
        code: "180600",
        name: "MUNICIPIO LUPERÓN",
        distritos: [
          { code: "180601", name: "LUPERÓN" },
          { code: "180602", name: "LA ISABELA (D. M.)." },
          { code: "180603", name: "BELLOSO (D. M.)." },
          {
            code: "180604",
            name: "EL ESTRECHO DE LUPERÓN OMAR BROSS (D. M.).",
          },
        ],
      },
      {
        code: "180700",
        name: "MUNICIPIO SOSÚA",
        distritos: [
          { code: "180701", name: "SOSÚA" },
          { code: "180702", name: "CABARETE (D. M.)." },
          { code: "180703", name: "SABANETA DE YÁSICA (D. M.)." },
        ],
      },
      {
        code: "180800",
        name: "MUNICIPIO VILLA ISABELA",
        distritos: [
          { code: "180801", name: "VILLA ISABELA" },
          { code: "180802", name: "ESTERO HONDO (D. M.)." },
          { code: "180803", name: "LA JAIBA (D. M.)." },
          { code: "180804", name: "GUALETE (D. M.)." },
        ],
      },
      {
        code: "180900",
        name: "MUNICIPIO VILLA MONTELLANO",
        distritos: [{ code: "180901", name: "VILLA MONTELLANO" }],
      },
    ],
  },
  {
    code: "190000",
    name: "PROVINCIA HERMANAS MIRABAL",
    municipios: [
      {
        code: "190100",
        name: "MUNICIPIO SALCEDO",
        distritos: [
          { code: "190101", name: "SALCEDO" },
          { code: "190102", name: "JAMAO AFUERA (D. M.)." },
        ],
      },
      {
        code: "190200",
        name: "MUNICIPIO TENARES",
        distritos: [
          { code: "190201", name: "TENARES" },
          { code: "190202", name: "BLANCO (D. M.)." },
        ],
      },
      {
        code: "190300",
        name: "MUNICIPIO VILLA TAPIA",
        distritos: [{ code: "190301", name: "VILLA TAPIA" }],
      },
    ],
  },
  {
    code: "200000",
    name: "PROVINCIA SAMANÁ",
    municipios: [
      {
        code: "200100",
        name: "MUNICIPIO SAMANÁ",
        distritos: [
          { code: "200101", name: "SAMANÁ" },
          { code: "200102", name: "EL LIMÓN  (D. M.)." },
          { code: "200103", name: "ARROYO BARRIL (D. M.)." },
          { code: "200104", name: "LAS GALERAS (D. M.)." },
        ],
      },
      {
        code: "200200",
        name: "MUNICIPIO SÁNCHEZ",
        distritos: [{ code: "200201", name: "SÁNCHEZ (D. M.)." }],
      },
      {
        code: "200300",
        name: "MUNICIPIO LAS TERRENAS",
        distritos: [{ code: "200301", name: "LAS TERRENAS" }],
      },
    ],
  },
  {
    code: "210000",
    name: "PROVINCIA SAN CRISTÓBAL",
    municipios: [
      {
        code: "210100",
        name: "MUNICIPIO SAN CRISTÓBAL",
        distritos: [
          { code: "210101", name: "SAN CRISTÓBAL (D. M.)." },
          { code: "210102", name: "HATO DAMAS (D. M.)." },
          { code: "210103", name: "HATILLO (D. M.)." },
        ],
      },
      {
        code: "210200",
        name: "MUNICIPIO SABANA GRANDE DE PALENQUE",
        distritos: [
          { code: "210201", name: "SABANA GRANDE DE PALENQUE (D. M.)." },
        ],
      },
      {
        code: "210300",
        name: "MUNICIPIO BAJOS DE HAINA",
        distritos: [
          { code: "210301", name: "BAJOS DE HAINA" },
          { code: "210302", name: "EL CARRIL (D. M.)." },
          { code: "210303", name: "QUITA SUEÑO (D. M.)." },
        ],
      },
      {
        code: "210400",
        name: "MUNICIPIO CAMBITA GARABITOS",
        distritos: [
          { code: "210401", name: "CAMBITA GARABITOS" },
          { code: "210402", name: "CAMBITA EL PUEBLECITO (D. M.)." },
        ],
      },
      {
        code: "210500",
        name: "MUNICIPIO VILLA ALTAGRACIA",
        distritos: [
          { code: "210501", name: "VILLA ALTAGRACIA" },
          { code: "210502", name: "SAN JOSÉ DEL PUERTO (D. M.)." },
          { code: "210503", name: "MEDINA (D. M.)." },
          { code: "210504", name: "LA CUCHILLA (D. M.)." },
        ],
      },
      {
        code: "210600",
        name: "MUNICIPIO YAGUATE",
        distritos: [
          { code: "210601", name: "YAGUATE (D. M.)." },
          { code: "210602", name: "DOÑA ANA (D. M.)" },
        ],
      },
      {
        code: "210700",
        name: "MUNICIPIO SAN GREGORIO DE NIGUA",
        distritos: [{ code: "210701", name: "SAN GREGORIO DE NIGUA" }],
      },
      {
        code: "210800",
        name: "MUNICIPIO LOS CACAOS",
        distritos: [{ code: "210801", name: "LOS CACAOS (D. M.)." }],
      },
    ],
  },
  {
    code: "220000",
    name: "PROVINCIA SAN JUAN",
    municipios: [
      {
        code: "220100",
        name: "MUNICIPIO SAN JUAN",
        distritos: [
          { code: "220101", name: "SAN JUAN" },
          { code: "220102", name: "PEDRO CORTO (D. M.)." },
          { code: "220103", name: "SABANETA (D. M.)." },
          { code: "220104", name: "SABANA ALTA (D. M.)." },
          { code: "220105", name: "EL ROSARIO (D. M.)." },
          { code: "220106", name: "HATO DEL PADRE (D. M.)." },
          { code: "220107", name: "GUANITO (D. M.)." },
          { code: "220108", name: "LA JAGUA (D. M.)." },
          { code: "220109", name: "LAS MAGUANAS-HATO NUEVO (D. M.)." },
          { code: "220110", name: "LAS CHARCAS DE MARÍA NOVA (D. M.)." },
          { code: "220111", name: "LAS ZANJAS (D. M.)" },
        ],
      },
      {
        code: "220200",
        name: "MUNICIPIO BOHECHÍO",
        distritos: [
          { code: "220201", name: "BOHECHÍO" },
          { code: "220202", name: "ARROYO CANO (D. M.)." },
          { code: "220203", name: "YAQUE (D. M.)." },
        ],
      },
      {
        code: "220300",
        name: "MUNICIPIO EL CERCADO",
        distritos: [
          { code: "220301", name: "EL CERCADO" },
          { code: "220302", name: "DERRUMBADERO (D. M.)" },
          { code: "220303", name: "BATISTA (D. M.)" },
        ],
      },
      {
        code: "220400",
        name: "MUNICIPIO JUAN DE HERRERA",
        distritos: [
          { code: "220401", name: "JUAN DE HERRERA" },
          { code: "220402", name: "JÍNOVA (D. M.)." },
        ],
      },
      {
        code: "220500",
        name: "MUNICIPIO LAS MATAS DE FARFÁN",
        distritos: [
          { code: "220501", name: "LAS MATAS DE FARFÁN" },
          { code: "220502", name: "MATAYAYA (D. M.)." },
          { code: "220503", name: "CARRERA DE YEGUAS (D. M.)." },
        ],
      },
      {
        code: "220600",
        name: "MUNICIPIO VALLEJUELO",
        distritos: [
          { code: "220601", name: "VALLEJUELO" },
          { code: "220602", name: "JORJILLO (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "230000",
    name: "PROVINCIA SAN PEDRO DE MACORÍS",
    municipios: [
      {
        code: "230100",
        name: "MUNICIPIO SAN PEDRO DE MACORÍS",
        distritos: [{ code: "230101", name: "SAN PEDRO DE MACORÍS" }],
      },
      {
        code: "230200",
        name: "MUNICIPIO LOS LLANOS",
        distritos: [
          { code: "230201", name: "LOS LLANOS" },
          { code: "230202", name: "EL PUERTO (D. M.)." },
          { code: "230203", name: "GAUTIER (D. M.)." },
        ],
      },
      {
        code: "230300",
        name: "MUNICIPIO RAMÓN SANTANA",
        distritos: [{ code: "230301", name: "RAMÓN SANTANA" }],
      },
      {
        code: "230400",
        name: "MUNICIPIO CONSUELO",
        distritos: [{ code: "230401", name: "CONSUELO" }],
      },
      {
        code: "230500",
        name: "MUNICIPIO QUISQUEYA",
        distritos: [{ code: "230501", name: "QUISQUEYA" }],
      },
      {
        code: "230600",
        name: "MUNICIPIO GUAYACANES",
        distritos: [{ code: "230601", name: "GUAYACANES" }],
      },
    ],
  },
  {
    code: "240000",
    name: "PROVINCIA SANCHEZ RAMÍREZ",
    municipios: [
      {
        code: "240100",
        name: "MUNICIPIO COTUÍ",
        distritos: [
          { code: "240101", name: "COTUÍ" },
          { code: "240102", name: "QUITA SUEÑO (D. M.)." },
          { code: "240103", name: "CABALLERO (D. M.)." },
          { code: "240104", name: "COMEDERO ARRIBA (D. M.)." },
          { code: "240105", name: "PLATANAL  (D. M.)." },
          { code: "240106", name: "ZAMBRANA ABAJO" },
        ],
      },
      {
        code: "240200",
        name: "MUNICIPIO CEVICOS",
        distritos: [
          { code: "240201", name: "CEVICOS" },
          { code: "240202", name: "LA CUEVA (D. M.)." },
        ],
      },
      {
        code: "240300",
        name: "MUNICIPIO FANTINO",
        distritos: [{ code: "240301", name: "FANTINO" }],
      },
      {
        code: "240400",
        name: "MUNICIPIO LA MATA",
        distritos: [
          { code: "240401", name: "LA MATA" },
          { code: "240402", name: "LA BIJA (D. M.)." },
          { code: "240403", name: "ANGELINA (D. M.)." },
          { code: "240404", name: "HERNANDO ALONZO (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "250000",
    name: "PROVINCIA SANTIAGO",
    municipios: [
      {
        code: "250100",
        name: "MUNICIPIO SANTIAGO",
        distritos: [
          { code: "250101", name: "SANTIAGO" },
          { code: "250102", name: "PEDRO GARCÍA (D. M.)." },
          { code: "250104", name: "BAITOA (D. M.)." },
          { code: "250105", name: "LA CANELA (D. M.)." },
          { code: "250106", name: "SAN FRANCISCO DE JACAGUA (D. M.)." },
          { code: "250107", name: "HATO DEL YAQUE (D. M.)." },
        ],
      },
      {
        code: "250200",
        name: "MUNICIPIO BISONÓ",
        distritos: [
          { code: "250201", name: "VILLA BISONÓ (NAVARRETE) (D. M.)." },
        ],
      },
      {
        code: "250300",
        name: "MUNICIPIO JÁNICO",
        distritos: [
          { code: "250301", name: "JÁNICO" },
          { code: "250302", name: "JUNCALITO (D. M.)." },
          { code: "250303", name: "EL CAIMITO (D. M.)." },
        ],
      },
      {
        code: "250400",
        name: "MUNICIPIO LICEY AL MEDIO",
        distritos: [
          { code: "250401", name: "LICEY AL MEDIO" },
          { code: "250402", name: "LAS PALOMAS (D. M.)." },
        ],
      },
      {
        code: "250500",
        name: "MUNICIPIO SAN JOSÉ DE LAS MATAS",
        distritos: [
          { code: "250501", name: "SAN JOSÉ DE LAS MATAS" },
          { code: "250502", name: "EL RUBIO (D. M.)." },
          { code: "250503", name: "LA CUESTA (D. M.)." },
          { code: "250504", name: "LAS PLACETAS (D. M.)." },
        ],
      },
      {
        code: "250600",
        name: "MUNICIPIO TAMBORIL",
        distritos: [
          { code: "250601", name: "TAMBORIL" },
          { code: "250602", name: "CANCA LA PIEDRA (D. M.)." },
        ],
      },
      {
        code: "250700",
        name: "MUNICIPIO VILLA GONZÁLEZ",
        distritos: [
          { code: "250701", name: "VILLA GONZÁLEZ" },
          { code: "250702", name: "PALMAR ARRIBA (D. M.)." },
          { code: "250703", name: "EL LIMÓN (D. M.)." },
        ],
      },
      {
        code: "250800",
        name: "MUNICIPIO PUÑAL",
        distritos: [
          { code: "250801", name: "PUÑAL" },
          { code: "250802", name: "GUAYABAL (D. M.)." },
          { code: "250803", name: "CANABACOA (D. M.)." },
        ],
      },
      {
        code: "250900",
        name: "MUNICIPIO SABANA IGLESIA",
        distritos: [
          { code: "250901", name: "SABANA IGLESIA" },
          { code: "251000", name: "BAITOA" },
          { code: "251001", name: "BAITOA" },
        ],
      },
    ],
  },
  {
    code: "260000",
    name: "PROVINCIA SANTIAGO RODRÍGUEZ",
    municipios: [
      {
        code: "260100",
        name: "MUNICIPIO SAN IGNACIO DE SABANETA",
        distritos: [
          { code: "260101", name: "SAN IGNACIO DE SABANETA (D. M.)." },
        ],
      },
      {
        code: "260200",
        name: "MUNICIPIO VILLA LOS ALMÁCIGOS",
        distritos: [{ code: "260201", name: "VILLA LOS ALMÁCIGOS (D. M.)." }],
      },
      {
        code: "260300",
        name: "MUNICIPIO MONCIÓN",
        distritos: [{ code: "260301", name: "MONCIÓN (D. M.)." }],
      },
    ],
  },
  {
    code: "270000",
    name: "PROVINCIA VALVERDE",
    municipios: [
      {
        code: "270100",
        name: "MUNICIPIO MAO",
        distritos: [
          { code: "270101", name: "MAO (D. M.)." },
          { code: "270102", name: "AMINA (D. M.)." },
          { code: "270103", name: "JAIBÓN (PUEBLO NUEVO) (D. M.)." },
          { code: "270104", name: "GUATAPANAL (D. M.)." },
        ],
      },
      {
        code: "270200",
        name: "MUNICIPIO ESPERANZA",
        distritos: [
          { code: "270201", name: "ESPERANZA" },
          { code: "270202", name: "MAIZAL (D. M.)." },
          { code: "270203", name: "JICOMÉ (D. M.)." },
          { code: "270204", name: "BOCA DE MAO (D. M.)." },
          { code: "270205", name: "PARADERO (D. M.)." },
        ],
      },
      {
        code: "270300",
        name: "MUNICIPIO LAGUNA SALADA",
        distritos: [
          { code: "270301", name: "LAGUNA SALADA (D. M.)." },
          { code: "270302", name: "JAIBÓN (D. M.)." },
          { code: "270303", name: "LA CAYA (D. M.)." },
          { code: "270304", name: "CRUCE DE GUAYACANES (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "280000",
    name: "PROVINCIA MONSEÑOR NOUEL",
    municipios: [
      {
        code: "280100",
        name: "MUNICIPIO BONAO",
        distritos: [
          { code: "280101", name: "BONAO (D. M.)." },
          { code: "280102", name: "SABANA DEL PUERTO (D. M.)." },
          { code: "280103", name: "JUMA BEJUCAL (D. M.)." },
          { code: "280104", name: "ARROYO  TORO - MASIPEDRO (D. M.)." },
          { code: "280105", name: "JAYACO (D. M.)." },
          { code: "280106", name: "LA SALVIA - LOS QUEMADOS (D. M.)." },
        ],
      },
      {
        code: "280200",
        name: "MUNICIPIO MAIMÓN",
        distritos: [{ code: "280201", name: "MAIMÓN (D. M.)." }],
      },
      {
        code: "280300",
        name: "MUNICIPIO PIEDRA BLANCA",
        distritos: [
          { code: "280301", name: "PIEDRA BLANCA (D. M.)." },
          { code: "280302", name: "VILLA DE SONADOR (D. M.)." },
          { code: "280303", name: "JUAN ADRIÁN (D. M.)." },
        ],
      },
    ],
  },
  {
    code: "290000",
    name: "PROVINCIA MONTE PLATA",
    municipios: [
      {
        code: "290100",
        name: "MUNICIPIO MONTE PLATA",
        distritos: [
          { code: "290101", name: "MONTE PLATA (D. M.)." },
          { code: "290102", name: "DON JUAN (D. M.)." },
          { code: "290103", name: "CHIRINO (D. M.)." },
          { code: "290104", name: "BOYÁ (D. M.)." },
        ],
      },
      {
        code: "290200",
        name: "MUNICIPIO BAYAGUANA",
        distritos: [{ code: "290201", name: "BAYAGUANA (D. M.)." }],
      },
      {
        code: "290300",
        name: "MUNICIPIO SABANA GRANDE DE BOYÁ",
        distritos: [
          { code: "290301", name: "SABANA GRANDE DE BOYÁ (D. M.)." },
          { code: "290302", name: "GONZALO (D. M.)." },
          { code: "290303", name: "MAJAGUAL (D. M.)." },
        ],
      },
      {
        code: "290400",
        name: "MUNICIPIO YAMASÁ",
        distritos: [
          { code: "290402", name: "LOS BOTADOS (D. M.)." },
          { code: "290403", name: "MAMÁ TINGÓ (D. M.)." },
        ],
      },
      {
        code: "290500",
        name: "MUNICIPIO PERALVILLO",
        distritos: [{ code: "290501", name: "PERALVILLO (D. M.)." }],
      },
    ],
  },
  {
    code: "300000",
    name: "PROVINCIA HATO MAYOR",
    municipios: [
      {
        code: "300100",
        name: "MUNICIPIO HATO MAYOR",
        distritos: [
          { code: "300101", name: "HATO MAYOR (D. M.)." },
          { code: "300102", name: "YERBA BUENA (D. M.)." },
          { code: "300103", name: "MATA PALACIO (D. M.)." },
          { code: "300104", name: "GUAYABO DULCE (D. M.)." },
        ],
      },
      {
        code: "300200",
        name: "MUNICIPIO SABANA DE LA MAR",
        distritos: [
          { code: "300201", name: "SABANA DE LA MAR (D. M.)." },
          { code: "300202", name: "ELUPINA CORDERO DE LAS CAÑITAS (D. M.)." },
        ],
      },
      {
        code: "300300",
        name: "MUNICIPIO EL VALLE",
        distritos: [{ code: "300301", name: "EL VALLE (D. M.)." }],
      },
    ],
  },
  {
    code: "310000",
    name: "PROVINCIA SAN JOSÉ DE OCOA",
    municipios: [
      {
        code: "310100",
        name: "MUNICIPIO SAN JOSÉ DE OCOA",
        distritos: [
          { code: "310101", name: "SAN JOSÉ DE OCOA (D. M.)." },
          { code: "310102", name: "LA CIÉNAGA (D. M.)." },
          { code: "310103", name: "NIZAO - LAS AUYAMAS (D. M.)." },
          { code: "310104", name: "EL PINAR (D. M.)." },
          { code: "310105", name: "EL NARANJAL (D. M.)." },
        ],
      },
      {
        code: "310200",
        name: "MUNICIPIO SABANA LARGA",
        distritos: [{ code: "310201", name: "SABANA LARGA (D. M.)." }],
      },
      {
        code: "310300",
        name: "MUNICIPIO RANCHO ARRIBA",
        distritos: [{ code: "310301", name: "RANCHO ARRIBA (D. M.)." }],
      },
    ],
  },
  {
    code: "320000",
    name: "PROVINCIA SANTO DOMINGO",
    municipios: [
      {
        code: "320100",
        name: "MUNICIPIO SANTO DOMINGO ESTE",
        distritos: [
          { code: "320101", name: "SANTO DOMINGO ESTE (D. M.)." },
          { code: "320102", name: "SAN LUIS (D. M.)." },
        ],
      },
      {
        code: "320200",
        name: "MUNICIPIO SANTO DOMINGO OESTE",
        distritos: [{ code: "320201", name: "SANTO DOMINGO OESTE (D. M.)." }],
      },
      {
        code: "320300",
        name: "MUNICIPIO SANTO DOMINGO NORTE",
        distritos: [
          { code: "320301", name: "SANTO DOMINGO NORTE (D. M.)." },
          { code: "320302", name: "LA VICTORIA (D. M.)." },
        ],
      },
      {
        code: "320400",
        name: "MUNICIPIO BOCA CHICA",
        distritos: [
          { code: "320401", name: "BOCA CHICA (D. M.)." },
          { code: "320402", name: "LA CALETA (D. M.)." },
        ],
      },
      {
        code: "320500",
        name: "MUNICIPIO SAN ANTONIO DE GUERRA",
        distritos: [
          { code: "320501", name: "SAN ANTONIO DE GUERRA (D. M.)." },
          { code: "320502", name: "HATO VIEJO (D. M.)." },
        ],
      },
      {
        code: "320600",
        name: "MUNICIPIO LOS ALCARRIZOS",
        distritos: [
          { code: "320601", name: "LOS ALCARRIZOS (D. M.)." },
          { code: "320602", name: "PALMAREJO-VILLA LINDA (D. M.)." },
          { code: "320603", name: "PANTOJA (D. M.)." },
        ],
      },
      {
        code: "320700",
        name: "MUNICIPIO PEDRO BRAND",
        distritos: [
          { code: "320701", name: "PEDRO BRAND (D. M.)." },
          { code: "320702", name: "LA GUÁYIGA (D. M.)." },
          { code: "320703", name: "LA CUABA (D. M.)." },
        ],
      },
    ],
  },
];

/** Busca una provincia por su código de 6 dígitos. */
export function findProvinciaByCode(code: string): Provincia | undefined {
  return PROVINCIAS.find((p) => p.code === code);
}

/**
 * Busca un municipio o distrito municipal por su código, en cualquier
 * provincia — el valor guardado puede ser de cualquiera de los dos niveles.
 */
export function findMunicipioOrDistritoByCode(
  code: string,
): { code: string; name: string } | undefined {
  for (const provincia of PROVINCIAS) {
    for (const municipio of provincia.municipios) {
      if (municipio.code === code) return municipio;
      const distrito = municipio.distritos.find((d) => d.code === code);
      if (distrito) return distrito;
    }
  }
  return undefined;
}

/** La provincia a la que pertenece un municipio o distrito municipal, si existe. */
export function findProvinciaForMunicipioCode(
  code: string,
): Provincia | undefined {
  return PROVINCIAS.find((p) =>
    p.municipios.some(
      (mu) => mu.code === code || mu.distritos.some((d) => d.code === code),
    ),
  );
}
