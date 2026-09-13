# Navegación PAM y base para colecciones

## Propósito

Este documento conserva el conocimiento operativo obtenido al automatizar el portal PAM y los contratos de las colecciones locales incorporadas después de la migración inicial. Las colecciones no existen en la aplicación anterior ni son una función nativa del portal.

No deben incluirse aquí credenciales, códigos TOTP, cookies, tokens, Request ID reales ni identidades reales observadas en el portal.

## Importante
Al momento de navegar en la aplicación web de BeyondInsight, puedes tomar capturas de pantalla de la web para que puedas observar de mejor manera como lucen las interfaces de la aplicación, sus nombres, como se estructura, etc. Tomar capturas de pantalla y luego visualizarlas puede ayudar mucho a entender mejor como se debe de navegar en la web. Puedes guardas las capturas de pantalla en esta carpeta "C:\pam automatizacion dotnet\PamAutomation\img" por si después necesitas visualizar otra vez las imagenes. Guarda las imagenes con un nombre descriptivo para que en un futuro sepas cual imagen buscar.

## Terminología exacta del portal

La documentación debe conservar en inglés los nombres mostrados por PAM. El grid de `Favorites` contiene estas columnas visibles confirmadas:

- `Account`
- `Description`
- `Directory`
- `System`
- `Account Status`
- `IP Address`
- `Platform`
- `Application`

En este documento, **`Favorites` row** significa una fila completa del grid y no solamente el valor de `Account`. El mismo `Account` puede aparecer más de una vez con distintos valores de `System` o `Application`; por eso `Account` no es una identidad única.

El modelo actual `PamFavoriteAccount` representa una `Favorites` row. Sus propiedades y selectores internos `AccountName`, `SystemName` y `ApplicationName` corresponden respectivamente a las columnas visibles `Account`, `System` y `Application`. Siempre que se describa un elemento del portal se usarán estos nombres ingleses entre código; la prosa española no debe traducirlos como si fueran campos distintos.

## Evidencia visual confirmada en fase 12

Las capturas de desarrollo se guardaron localmente en `img` y permanecen excluidas de Git:

- `fase12-request-form-before-submit.png`: panel `Access` antes del único envío autorizado.
- `fase12-request-result-after-submit.png`: grid de `Favorites` y notificación `Successfully submitted request.` después de la respuesta exitosa.

La navegación visible confirmó:

- Pestañas principales `Accounts` y `Requests`.
- Categorías `Favorites`, `Recently Used`, `Local Accounts`, `Directory Linked Accounts` y `Applications` bajo `Browse by Category`.
- Controles `Filter by`, `Quick filter`, `items per page`, `First page` y `Next page`.
- Columnas visibles `Directory`, `Account`, `Description`, `System`, `Account Status`, `IP Address`, `Platform` y `Application`; algunas requieren desplazamiento horizontal.
- Una acción `Access` por `Favorites` row.
- Panel `Access` con pestañas `Submit Request` y `Direct Connect`.
- Resumen de identidad mediante `Account`, `System` y `Application` antes de mostrar el formulario.
- Pregunta `What type of access do you need?` con `Application Session` seleccionado.
- Sección `What are the details of this request?` con `Reason`, `Ticket System (optional)` y `Ticket Number (optional)`, además de los campos temporales ya confirmados en fase 11.
- Botones finales `Submit Request` y `Cancel`.
- Notificación de éxito `Successfully submitted request.` con acciones `Pause`, `Resume`, `Dismiss All` y `Dismiss`.

La captura mostró 14 elementos en una sola página durante esa sesión, pero este valor es evidencia puntual, no una constante del sistema. La automatización debe seguir leyendo el conteo y la paginación dinámicamente.

La única prueba destructiva autorizada confirmó que el portal aceptó `Days = 0`, `Hours = 0` y `Minutes = 5`. El inicio introducido como `08:20 AM` fue verificado con semántica de 24 horas como `08:20`. Estos minutos son únicamente una duración técnica mínima para validación; la configuración operativa de la aplicación continúa expresándose en horas.

## Evidencia visual confirmada en fase 13

Las capturas locales `img/fase13-pending-grid.png`, `img/fase13-pending-grid-description-and-expiration.png` e `img/fase13-pending-grid-application.png`, excluidas de Git, confirmaron la pestaña `Requests` con:

- Filtro `Status` seleccionado en `Pending` y control `Filter by`.
- Tanto el estado vacío `0 items` y `There are no records to display.` como filas `Pending` reales.
- Encabezados `Request ID`, `System`, `Type`, `Directory`, `Account`, `Request Start Date`, `Response`, `Response Date`, `Expires`, `Description`, `Ticket`, `Application` y `Location`.
- Paginador, controles anterior/siguiente, selector `items per page` y contador dinámico.

Las filas técnicas autorizadas de 5 minutos confirmaron los identificadores DOM `RequestId`, `SystemName`, `AccessType`, `DomainName`, `AccountName`, `ReleaseDt`, `ResponseStatus`, `ResponseDt`, `ExpiresDt`, `Description`, `TicketNumber`, `Application`, `Location` y `RowContextMenu`. Para observar la tabla completa, la automatización usa `ScrollIntoViewIfNeededAsync` sobre una celda de `Description` y otra de `Application` y toma capturas completas después de cada desplazamiento horizontal. Las imágenes son evidencia local de desarrollo y pueden contener Request ID e identidades operativas; no se versionan ni se reproducen en este documento.

La observación real confirmó que la columna `Application` de Pending puede usar una nomenclatura distinta de Favorites: `NOSQLBOOSTER` aparece como `NOSQLBOOSTER - …`, `PLSQL - Nodo1` como `PLSQL - BDMOVIL-…`, `PuTTY - Nodo2` como `PuTTY 0.77 - …` y `WinSCP - nodo1` como `WinSCP 5.21 N2 - …`. La regla exige `System` y `Account` exactos, la misma familia controlada de `Application`, el mismo `Request Start Date`, el `Expires` calculado desde la duración y la misma `Description`. No se deriva la familia desde `System`, no se utiliza `Ticket` para asociar y no se consulta `Approved` para completar la comparación. Dos filas de una misma familia bajo el mismo `System + Account` siguen siendo ambiguas.

## Recorrido web confirmado

Toda la navegación de creación de solicitudes ocurre en un único contexto temporal y oculto de Microsoft Playwright para .NET, usando el Edge externo en modo `headless` y restringido al origen exacto `https://10.30.17.117`.

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
15. En modo de prueba, verificar todos los valores y ejecutar `Cancel` seguido de `Discard Changes`. En modo real, la confirmación general ocurre una sola vez al pulsar `Iniciar automatización`; después, la validación inmediata debe preceder a cada `Submit Request` sin mostrar otro diálogo general.
16. Para leer resultados, navegar a `/webconsole/ps/portal?tab=requests`, exigir un único control `Status`, seleccionar la opción exacta `Pending` y comprobar que el control conserva ese valor.
17. Recorrer el paginador desde la primera página, validar Request ID numéricos y únicos y comparar el contador dinámico cuando esté disponible. `0 items` es un resultado válido.
18. Asociar primero por `Request Start Date + System + Account`; validar después `Application`, `Expires` y `Description`. Nunca asociar por Request ID supuesto, orden o texto truncado visible.
19. Después de que el usuario acepte la confirmación inicial y antes de enviar solicitudes reales, leer todo `Pending` y guardar en memoria sus Request ID como línea base. Si existen filas con `System`, `Account`, familia de `Application`, `Request Start Date`, `Expires` y `Description` iguales, informar la cantidad y exigir que el usuario elija volver a pedirlas o cancelar; esta segunda pregunta aparece solo porque la decisión es necesaria.
20. Si el usuario acepta repetirlas, enviar normalmente y excluir la línea base de la reconciliación posterior. Las filas nuevas deben seguir siendo únicas por cada `Favorites` row; una duplicación surgida después del preflight continúa siendo ambigua y bloqueante.

## Acceso interactivo persistente a PAM

El botón `Acceder a PAM` es una excepción deliberada al contexto temporal anterior. Su único objetivo automatizado es abrir BeyondInsight e iniciar sesión; después entrega al usuario una ventana normal de Microsoft Edge, sin restricciones de navegación y capaz de permanecer abierta cuando se cierre PAM Automation.

1. Abrir `msedge.exe --new-window` sin `userDataDir`, depuración remota ni un perfil dedicado. Edge utiliza el perfil normal del usuario, incluidos sus manejadores de descarga y asociaciones de archivos.
2. Localizar únicamente ventanas del proceso `msedge` mediante Windows UI Automation y validar que la barra de direcciones pertenezca exactamente al origen `https://10.30.17.117`.
3. Si Edge muestra `Su conexión no es privada`, no omitir el certificado por código. Mostrar instrucciones y esperar hasta tres minutos para que el usuario pulse manualmente `Avanzado` y `Continuar a 10.30.17.117 (no seguro)`; continuar solo cuando aparezca `Username` o una sesión PAM autenticada.
4. Si el perfil normal conserva una sesión válida y la barra de direcciones ya está fuera de `/login`, confirmarla sin volver a enviar usuario, contraseña ni TOTP.
5. Si se muestra el login, automatizar solo los controles accesibles observados: `Username`, `Password`, `Log In`, `Enter Response` y `Submit`.
6. Obtener el `Date` HTTP mediante una petición auxiliar cuya excepción HTTPS acepta exclusivamente el origen PAM; esta petición no altera la seguridad del navegador y solo calibra TOTP.
7. Confirmar por la barra de direcciones que Edge salió de `/login` y liberar la referencia de accesibilidad sin cerrar la ventana ni el proceso.
8. Con la ventana PAM autenticada y su barra de direcciones enfocada, enviar `Ctrl+0` y cuatro veces `Ctrl+-`; exigir que el panel accesible de Edge confirme `67 %`. El foco y la validación exacta del origen evitan aplicar teclas sobre otra ventana o sitio.
9. No navegar automáticamente a `Favorites`, formularios de solicitud, `Pending` ni correo. Desde ese momento toda navegación es manual y no está limitada al origen PAM.

La decisión de confiar en el certificado pertenece al usuario y queda persistida por el navegador normal según sus propias políticas. La contraseña, la semilla y los códigos TOTP no se escriben en el perfil ni en logs. Esta excepción de uso del perfil normal se limita a `Acceder a PAM`; la automatización de solicitudes continúa usando Playwright con un contexto temporal, aislado y restringido al origen autorizado.

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

La fase 20.1 crea una ventana propia `Colecciones`. Su primer estado está vacío y ofrece `Obtener solicitudes favoritas`; después de una lectura completa y exitosa el botón cambia permanentemente a `Actualizar solicitudes favoritas`. La lista visible se reduce deliberadamente a `Application - IP Address`, aunque el snapshot protegido conserva la identidad completa `System + Application + Account` necesaria para las fases siguientes.

El encabezado visible `IP Address` utiliza en el DOM real `data-column-id="SystemIP"`. La primera validación de fase 20.1 demostró que suponer literalmente `IPAddress` impide completar las filas aunque `Favorites` haya cargado. Debe mantenerse la separación entre el nombre visible documentado en inglés y el identificador técnico confirmado del grid.

La pantalla de colecciones:

- Mostrar el snapshot completo y actualizado de Favorites con selección individual.
- Conservar en el selector los encabezados del portal `Directory`, `Account`, `Description`, `System`, `Account Status`, `IP Address`, `Platform` y `Application`; una traducción de interfaz podrá acompañarlos, pero no reemplazar los nombres técnicos en documentación o diagnóstico.
- Permitir seleccionar únicamente filas cuyo `Account Status` sea `Available`; los demás estados deberán mostrarse sin reinterpretarlos.
- Permite crear y modificar colecciones personalizadas con nombre, razón y selección de `Favorites` rows.
- Muestra cuántas solicitudes contiene cada colección y cuál está activa mediante un toggle exclusivo.
- Reserva `All favorites` para la aplicación: su nombre y solicitudes no son editables; el usuario solo modifica la razón.
- Sincroniza `All favorites` con todas las filas tras cada `Obtener/Actualizar solicitudes favoritas`.
- Mantiene fecha, hora y duración fuera de las colecciones. Esos valores se editan en `Editar solicitud` para la colección activa.

Entrar al selector de `Favorites` rows requerirá actualizar `Favorites` mediante una sesión autenticada. Si PAM no está disponible, la aplicación podrá mostrar los datos locales protegidos, pero deberá distinguir claramente que `Account Status` no fue comprobado y bloquear tanto la ejecución como los cambios de selección que dependan del snapshot vivo.

## Modelo y reconciliación

Cada colección tiene un identificador local estable independiente de su nombre, una lista ordenada de claves de `Favorites` rows y una razón. Las colecciones personalizadas permiten editar nombre, selección y razón. `All favorites` conserva un indicador administrado que impide modificar su nombre o selección fuera de la reconciliación.

La combinación `System + Application + Account` es la mejor clave confirmada hasta la fase 11, pero no debe asumirse inmutable para siempre. Si las fases 12 o 13 descubren un identificador estable del portal o demuestran que otra columna —por ejemplo `Platform`— forma parte de la identidad, la fase 20 deberá ampliar la clave mediante una migración de esquema explícita, nunca reinterpretar silenciosamente datos guardados.

`Favorites` seguirá siendo la fuente de verdad sobre disponibilidad. Al abrir o ejecutar una colección, la aplicación deberá volver a leer todas las páginas y reconciliar cada clave guardada:

- **`Available`:** existe exactamente una coincidencia y su `Account Status` es `Available`; puede mantenerse seleccionada.
- **`Missing`:** la clave guardada ya no aparece en `Favorites`; durante una actualización se retirará automáticamente de las colecciones que la contengan y se notificará cuántas selecciones fueron eliminadas.
- **`Ambiguous`:** aparece más de una coincidencia exacta; toda la colección debe bloquearse hasta resolver la inconsistencia.
- **`New in Favorites`:** la fila aparece en el snapshot vivo pero no en una colección personalizada; se ofrece para añadir, pero nunca se incorpora automáticamente. En `All favorites` sí se incorpora automáticamente.

No se sustituirá una `Favorites` row eliminada por otra con `Account`, `System` o `Application` parecido. En colecciones personalizadas, las eliminaciones automáticas se limitan a claves exactas ausentes y las filas nuevas requieren acción explícita. En `All favorites`, tanto altas como bajas exactas se sincronizan automáticamente porque esa colección representa siempre el catálogo completo.

## Persistencia y seguridad

Las colecciones contendrán identidades de `Favorites` rows y razones de negocio, por lo que se tratarán como configuración privada. Se guardarán bajo `%LOCALAPPDATA%\PamAutomation\config`, con `schemaVersion`, escritura atómica y protección DPAPI `CurrentUser`. No se almacenarán credenciales, TOTP, cookies, estados de sesión ni resultados de ejecución dentro de una colección.

El catálogo previo de fase 20.1 se guarda aparte como `favorite-catalog.dat`, también versionado, atómico y protegido con DPAPI `CurrentUser`. Al abrirse puede mostrar la última copia local, pero solo una actualización PAM exitosa podrá autorizar cambios posteriores de selección o ejecución.

`Directory`, `Description`, `Account Status`, `IP Address` y `Platform` se consideran metadata viva para presentación y reconciliación. No deben duplicarse de forma permanente dentro de la colección salvo que una necesidad funcional futura lo justifique; en particular, `IP Address` no forma parte de la clave actual.

La eliminación deberá ser explícita y recuperable durante la sesión de edición cuando resulte práctico. Los resultados y Request ID producidos al ejecutar una colección seguirán únicamente en memoria conforme a las reglas generales del proyecto.

Las fases 20.2–20.4 materializan el editor con nombre, checklist `Application - IP Address` y razón. La ventana de Colecciones muestra cada definición y un toggle exclusivo para escoger la colección activa. No existe un toggle de modo en la portada: toda automatización usa esa colección. `Editar solicitud` configura únicamente fecha, hora `hh:mm AM/PM` y duración.

La ejecución nunca utiliza los índices ni una copia ciega del catálogo. Resuelve `System + Application + Account` contra una lectura viva antes del preflight de `Pending` y nuevamente en el límite irreversible de envío. Una ausencia o diferencia cancela antes de crear solicitudes y pide actualizar `Favorites`.

## Validación mínima

- Reconciliación determinista de `Favorites` rows con estados `Available`, `Missing`, `New in Favorites` y `Ambiguous`.
- Selección conservada al filtrar y paginar.
- Conversión correcta entre entrada `AM/PM`, modelo temporal y valor normalizado por PAM.
- Separación entre razón de la colección y configuración temporal común sin mutaciones cruzadas.
- Persistencia versionada, protegida y atómica sin secretos.
- Confirmación que muestre nombre de colección y cantidad exacta de solicitudes antes de una ejecución real.
- Pruebas de modo seguro que recorran únicamente las `Favorites` rows seleccionadas y mantengan `submitted = 0`.
- Pruebas reales limitadas y explícitamente autorizadas; nunca asumir que crear o editar una colección autoriza enviar solicitudes.
