# Navegación PAM y base para colecciones

## Propósito

Este documento conserva el conocimiento operativo obtenido al automatizar el portal PAM y define las consideraciones de una característica futura de colecciones. Las colecciones no existen en la aplicación anterior y no forman parte de la migración inicial; se implementarán después de completar y validar las fases actuales.

No deben incluirse aquí credenciales, códigos TOTP, cookies, tokens, Request ID reales ni identidades reales observadas en el portal.

## Terminología exacta del portal

La documentación debe conservar en inglés los nombres mostrados por PAM. El grid de `Favorites` contiene estas columnas visibles confirmadas:

- `Account`
- `Description`
- `System`
- `Account Status`
- `IP Address`
- `Platform`
- `Application`

En este documento, **`Favorites` row** significa una fila completa del grid y no solamente el valor de `Account`. El mismo `Account` puede aparecer más de una vez con distintos valores de `System` o `Application`; por eso `Account` no es una identidad única.

El modelo actual `PamFavoriteAccount` representa una `Favorites` row. Sus propiedades y selectores internos `AccountName`, `SystemName` y `ApplicationName` corresponden respectivamente a las columnas visibles `Account`, `System` y `Application`. Siempre que se describa un elemento del portal se usarán estos nombres ingleses entre código; la prosa española no debe traducirlos como si fueran campos distintos.

## Recorrido web confirmado

Toda la navegación ocurre en un único contexto temporal de Microsoft Playwright para .NET, usando el Edge externo visible y restringido al origen exacto `https://10.30.17.117`.

1. Abrir `/webconsole/login?redir=dashboard`.
2. Completar usuario y contraseña mediante los controles accesibles `Username` y `Password` y pulsar `Log In`.
3. Esperar el control `Enter Response`, calcular un TOTP con la hora HTTP confiable del portal y pulsar `Submit`.
4. Confirmar la sesión mediante el enlace accesible `Return to dashboard`.
5. Navegar en la misma sesión a `/webconsole/ps/portal?tab=accounts`.
6. Esperar que exista una única sección visible `Browse by Category`.
7. Seleccionar una única categoría visible `Favorites`.
8. Esperar que el grid exponga filas estables mediante `tr[aria-label^="Row "]`.
9. Leer cada identidad desde las columnas visibles `System`, `Application` y `Account`; en el DOM actual corresponden a `SystemName`, `ApplicationName` y `AccountName`.
10. Si aparece `input[type="number"][aria-label^="Page "]`, interpretar su etiqueta `Page actual/total`, volver a `First page` cuando sea necesario y avanzar únicamente con `Next page`. El límite defensivo actual es de 500 páginas.
11. Para abrir una solicitud, volver a localizar la fila por su identidad completa, exigir una única acción `Access`, abrirla y seleccionar la pestaña accesible `Submit Request`.
12. Esperar `Select session start date` y los campos `Start Date`, `Start Time`, `Days`, `Hours`, `Minutes`, `Reason` y el switch `Application Session`.
13. Seleccionar la fecha mediante la interfaz visible de Flatpickr. El calendario puede abrirse automáticamente o ignorar ocasionalmente el primer clic; la automatización debe comprobar si ya está visible y usar reintentos breves sin manipular el DOM.
14. Entregar la hora según el formato aceptado por el portal y verificar el valor normalizado antes de continuar. El portal termina representando la hora con semántica de 24 horas aunque la futura interfaz de usuario use entrada de 12 horas con `AM/PM`.
15. En modo de prueba, verificar todos los valores y ejecutar `Cancel` seguido de `Discard Changes`. En modo real, una confirmación explícita y una validación inmediata deben preceder al botón final `Submit Request`.

## Invariantes que deben conservarse

- Nunca identificar una `Favorites` row solo por índice, posición visual, página, `Account` o texto parcial. El orden y la paginación pueden cambiar.
- La clave lógica actual es la combinación exacta y completa `System + Application + Account`.
- Leer primero un snapshot estable y único; antes de abrir cada formulario, volver a leer la fila y comparar su identidad.
- Tratar filas incompletas, identidades duplicadas, paginación cambiante, controles ambiguos o valores restablecidos como fallos bloqueantes.
- Priorizar roles, labels y atributos estables. Los selectores CSS se limitan al grid y a Flatpickr cuando no existe una alternativa accesible suficiente.
- Mantener autenticación, Favorites y formularios en la misma sesión temporal. No reutilizar perfiles, cookies ni sesiones persistentes de Edge.
- No imprimir ni persistir las identidades descubiertas como diagnóstico ordinario.
- Una colección local nunca debe modificar la categoría Favorites del portal ni presentarse como si fuera una colección nativa de PAM.
- Al ejecutar una colección, procesar únicamente sus `Favorites` rows seleccionadas y volver a localizar cada una por su clave en la paginación viva; no convertir la colección en una selección por rangos o posiciones.

## Característica post-migración: colecciones personalizadas

Una colección será una selección local, editable y nombrada de `Favorites` rows. Su objetivo es permitir, por ejemplo, escoger 8 filas de las 20 mostradas en `Favorites` y ejecutar solo ese subconjunto bajo un nombre reconocible como `Accesos con el Tic Peter`.

La pantalla de edición deberá:

- Mostrar el snapshot completo y actualizado de Favorites con selección individual.
- Conservar en el selector los encabezados del portal `Account`, `Description`, `System`, `Account Status`, `IP Address`, `Platform` y `Application`; una traducción de interfaz podrá acompañarlos, pero no reemplazar los nombres técnicos en documentación o diagnóstico.
- Permitir seleccionar únicamente filas cuyo `Account Status` sea `Available`; los demás estados deberán mostrarse sin reinterpretarlos.
- Permitir crear, renombrar, duplicar, modificar y eliminar colecciones.
- Mostrar cuántas `Favorites` rows tienen `Account Status = Available` y cuántas están seleccionadas para la colección.
- Permitir buscar o filtrar sin perder la selección de otras páginas.
- Guardar valores predeterminados de fecha, hora, duración y razón para la colección.
- Presentar la fecha mediante día y mes y una descripción completa, como en la referencia visual aportada.
- Presentar la hora en formato de 12 horas con selector `AM/PM`; el dominio continuará usando una representación temporal inequívoca y la conversión al formato del portal se verificará antes del envío.
- Presentar duración operativa en horas, aunque las integraciones técnicas autorizadas puedan utilizar minutos mínimos.
- Permitir opcionalmente sobrescribir fecha, hora, duración o razón para solicitudes individuales asociadas a una `Favorites` row. Esta capacidad se considera planificada y deberá confirmarse al diseñar la fase.

Entrar al selector de `Favorites` rows requerirá actualizar `Favorites` mediante una sesión autenticada. Si PAM no está disponible, la aplicación podrá mostrar los datos locales protegidos, pero deberá distinguir claramente que `Account Status` no fue comprobado y bloquear tanto la ejecución como los cambios de selección que dependan del snapshot vivo.

## Modelo y reconciliación previstos

Cada colección deberá tener un identificador local estable independiente de su nombre, un nombre editable, una lista ordenada de claves de `Favorites` rows y una configuración predeterminada de solicitud. Los posibles valores específicos de una fila se modelarán como sobrescrituras, no como copias completas de la configuración general.

La combinación `System + Application + Account` es la mejor clave confirmada hasta la fase 11, pero no debe asumirse inmutable para siempre. Si las fases 12 o 13 descubren un identificador estable del portal o demuestran que otra columna —por ejemplo `Platform`— forma parte de la identidad, la fase 20 deberá ampliar la clave mediante una migración de esquema explícita, nunca reinterpretar silenciosamente datos guardados.

`Favorites` seguirá siendo la fuente de verdad sobre disponibilidad. Al abrir o ejecutar una colección, la aplicación deberá volver a leer todas las páginas y reconciliar cada clave guardada:

- **`Available`:** existe exactamente una coincidencia y su `Account Status` es `Available`; puede mantenerse seleccionada.
- **`Missing`:** la clave guardada ya no aparece en `Favorites`; debe mostrarse al usuario y quedar bloqueada para ejecución.
- **`Ambiguous`:** aparece más de una coincidencia exacta; toda la colección debe bloquearse hasta resolver la inconsistencia.
- **`New in Favorites`:** la fila aparece en el snapshot vivo pero no en la colección; se ofrece para añadir, pero nunca se incorpora automáticamente.

No se sustituirá una `Favorites` row eliminada por otra con `Account`, `System` o `Application` parecido ni se actualizará una identidad silenciosamente. Los cambios de selección o configuración deberán guardarse solo mediante una acción explícita del usuario.

## Persistencia y seguridad previstas

Las colecciones contendrán identidades de `Favorites` rows y razones de negocio, por lo que se tratarán como configuración privada. Se guardarán bajo `%LOCALAPPDATA%\PamAutomation\config`, con `schemaVersion`, escritura atómica y protección DPAPI `CurrentUser`. No se almacenarán credenciales, TOTP, cookies, estados de sesión ni resultados de ejecución dentro de una colección.

`Description`, `Account Status`, `IP Address` y `Platform` se consideran metadata viva para presentación y reconciliación. No deben duplicarse de forma permanente dentro de la colección salvo que una necesidad funcional futura lo justifique; en particular, `IP Address` no forma parte de la clave actual.

La eliminación deberá ser explícita y recuperable durante la sesión de edición cuando resulte práctico. Los resultados y Request ID producidos al ejecutar una colección seguirán únicamente en memoria conforme a las reglas generales del proyecto.

## Validación futura mínima

- Reconciliación determinista de `Favorites` rows con estados `Available`, `Missing`, `New in Favorites` y `Ambiguous`.
- Selección conservada al filtrar y paginar.
- Conversión correcta entre entrada `AM/PM`, modelo temporal y valor normalizado por PAM.
- Configuración general y sobrescrituras individuales sin mutaciones cruzadas.
- Persistencia versionada, protegida y atómica sin secretos.
- Confirmación que muestre nombre de colección y cantidad exacta de solicitudes antes de una ejecución real.
- Pruebas de modo seguro que recorran únicamente las `Favorites` rows seleccionadas y mantengan `submitted = 0`.
- Pruebas reales limitadas y explícitamente autorizadas; nunca asumir que crear o editar una colección autoriza enviar solicitudes.
