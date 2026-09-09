# Navegación PAM y base para colecciones

## Propósito

Este documento conserva el conocimiento operativo obtenido al automatizar el portal PAM y define las consideraciones de una característica futura de colecciones. Las colecciones no existen en la aplicación anterior y no forman parte de la migración inicial; se implementarán después de completar y validar las fases actuales.

No deben incluirse aquí credenciales, códigos TOTP, cookies, tokens, Request ID reales ni identidades reales observadas en el portal.

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
9. Leer cada identidad desde las columnas `SystemName`, `ApplicationName` y `AccountName`.
10. Si aparece `input[type="number"][aria-label^="Page "]`, interpretar su etiqueta `Page actual/total`, volver a `First page` cuando sea necesario y avanzar únicamente con `Next page`. El límite defensivo actual es de 500 páginas.
11. Para abrir una solicitud, volver a localizar la fila por su identidad completa, exigir una única acción `Access`, abrirla y seleccionar la pestaña accesible `Submit Request`.
12. Esperar `Select session start date` y los campos `Start Date`, `Start Time`, `Days`, `Hours`, `Minutes`, `Reason` y el switch `Application Session`.
13. Seleccionar la fecha mediante la interfaz visible de Flatpickr. El calendario puede abrirse automáticamente o ignorar ocasionalmente el primer clic; la automatización debe comprobar si ya está visible y usar reintentos breves sin manipular el DOM.
14. Entregar la hora según el formato aceptado por el portal y verificar el valor normalizado antes de continuar. El portal termina representando la hora con semántica de 24 horas aunque la futura interfaz de usuario use entrada de 12 horas con `AM/PM`.
15. En modo de prueba, verificar todos los valores y ejecutar `Cancel` seguido de `Discard Changes`. En modo real, una confirmación explícita y una validación inmediata deben preceder al botón final `Submit Request`.

## Invariantes que deben conservarse

- Nunca identificar una cuenta solo por índice, posición visual, página o texto parcial. El orden y la paginación pueden cambiar.
- La clave lógica actual es la combinación exacta y completa de sistema, aplicación y cuenta.
- Leer primero un snapshot estable y único; antes de abrir cada formulario, volver a leer la fila y comparar su identidad.
- Tratar filas incompletas, identidades duplicadas, paginación cambiante, controles ambiguos o valores restablecidos como fallos bloqueantes.
- Priorizar roles, labels y atributos estables. Los selectores CSS se limitan al grid y a Flatpickr cuando no existe una alternativa accesible suficiente.
- Mantener autenticación, Favorites y formularios en la misma sesión temporal. No reutilizar perfiles, cookies ni sesiones persistentes de Edge.
- No imprimir ni persistir las identidades descubiertas como diagnóstico ordinario.
- Una colección local nunca debe modificar la categoría Favorites del portal ni presentarse como si fuera una colección nativa de PAM.
- Al ejecutar una colección, procesar únicamente sus claves seleccionadas y volver a localizar cada una en la paginación viva; no convertir la colección en una selección por rangos o posiciones.

## Característica post-migración: colecciones personalizadas

Una colección será una selección local, editable y nombrada de cuentas que actualmente aparecen en Favorites. Su objetivo es permitir, por ejemplo, escoger 8 cuentas de 20 favoritas y ejecutar solo ese subconjunto bajo un nombre reconocible como `Accesos con el Tic Peter`.

La pantalla de edición deberá:

- Mostrar el snapshot completo y actualizado de Favorites con selección individual.
- Permitir crear, renombrar, duplicar, modificar y eliminar colecciones.
- Mostrar cuántas cuentas están disponibles y cuántas están seleccionadas.
- Permitir buscar o filtrar sin perder la selección de otras páginas.
- Guardar valores predeterminados de fecha, hora, duración y razón para la colección.
- Presentar la fecha mediante día y mes y una descripción completa, como en la referencia visual aportada.
- Presentar la hora en formato de 12 horas con selector `AM/PM`; el dominio continuará usando una representación temporal inequívoca y la conversión al formato del portal se verificará antes del envío.
- Presentar duración operativa en horas, aunque las integraciones técnicas autorizadas puedan utilizar minutos mínimos.
- Permitir opcionalmente sobrescribir fecha, hora, duración o razón para solicitudes individuales dentro de la colección. Esta capacidad se considera planificada y deberá confirmarse al diseñar la fase.

Entrar al selector de cuentas requerirá actualizar Favorites mediante una sesión autenticada. Si PAM no está disponible, la aplicación podrá mostrar los datos locales protegidos, pero deberá distinguir claramente que su disponibilidad no fue comprobada y bloquear tanto la ejecución como los cambios de selección que dependan del snapshot vivo.

## Modelo y reconciliación previstos

Cada colección deberá tener un identificador local estable independiente de su nombre, un nombre editable, una lista ordenada de claves de cuenta y una configuración predeterminada de solicitud. Los posibles valores específicos de una cuenta se modelarán como sobrescrituras, no como copias completas de la configuración general.

La combinación sistema, aplicación y cuenta es la mejor clave confirmada hasta la fase 11, pero no debe asumirse inmutable para siempre. Si las fases 12 o 13 descubren un identificador estable del portal o demuestran que otra columna forma parte de la identidad, la fase 20 deberá ampliar la clave mediante una migración de esquema explícita, nunca reinterpretar silenciosamente datos guardados.

Favorites seguirá siendo la fuente de verdad sobre disponibilidad. Al abrir o ejecutar una colección, la aplicación deberá volver a leer todas las páginas y reconciliar cada clave guardada:

- **Disponible:** existe exactamente una coincidencia; puede mantenerse seleccionada.
- **No disponible:** ya no aparece en Favorites; debe mostrarse al usuario y quedar bloqueada para ejecución.
- **Ambigua:** aparece más de una coincidencia exacta; toda la colección debe bloquearse hasta resolver la inconsistencia.
- **Nueva en Favorites:** se muestra como disponible para añadir, pero nunca se incorpora automáticamente.

No se sustituirá una cuenta eliminada por otra con nombre parecido ni se actualizará una identidad silenciosamente. Los cambios de selección o configuración deberán guardarse solo mediante una acción explícita del usuario.

## Persistencia y seguridad previstas

Las colecciones contendrán identidades de cuentas y razones de negocio, por lo que se tratarán como configuración privada. Se guardarán bajo `%LOCALAPPDATA%\PamAutomation\config`, con `schemaVersion`, escritura atómica y protección DPAPI `CurrentUser`. No se almacenarán credenciales, TOTP, cookies, estados de sesión ni resultados de ejecución dentro de una colección.

La eliminación deberá ser explícita y recuperable durante la sesión de edición cuando resulte práctico. Los resultados y Request ID producidos al ejecutar una colección seguirán únicamente en memoria conforme a las reglas generales del proyecto.

## Validación futura mínima

- Reconciliación determinista frente a cuentas disponibles, ausentes, nuevas y duplicadas.
- Selección conservada al filtrar y paginar.
- Conversión correcta entre entrada `AM/PM`, modelo temporal y valor normalizado por PAM.
- Configuración general y sobrescrituras individuales sin mutaciones cruzadas.
- Persistencia versionada, protegida y atómica sin secretos.
- Confirmación que muestre nombre de colección y cantidad exacta de solicitudes antes de una ejecución real.
- Pruebas de modo seguro que recorran únicamente las cuentas seleccionadas y mantengan `submitted = 0`.
- Pruebas reales limitadas y explícitamente autorizadas; nunca asumir que crear o editar una colección autoriza enviar solicitudes.
