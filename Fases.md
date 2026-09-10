# Fases de desarrollo — Pam Automation .NET

Este documento resume las fases implementadas, dónde se incorporó cada cambio y por qué fue necesario. El detalle cronológico permanece en `History.md` y la estructura física en `structure.md`.

## Fase 1 — Estructura base

Se creó `PamAutomation.slnx` con los proyectos `App`, `Core`, `Browser`, `Infrastructure`, `Outlook` y `Tests`, todos orientados a .NET 10 y x64. Esta separación estableció desde el inicio los límites entre interfaz, dominio e integraciones externas, evitando concentrar toda la aplicación en WPF.

## Fase 2 — Presentación WPF y MVVM

Se implementó la ventana principal en `PamAutomation.App` con WPF/XAML, recursos visuales y `MainWindowViewModel`. Los estados seguros y comandos quedaron fuera del code-behind y se cubrieron con pruebas. Esta fase creó la base de interacción sin activar todavía PAM, Edge u Outlook.

## Fase 2B — Alineación visual

Se reconstruyó la portada nativa tomando como referencia visual la aplicación anterior. Se conservaron encabezado, jerarquía de acciones y accesos de configuración sin copiar HTML, CSS o JavaScript. Fue importante para validar tempranamente la dirección visual antes de conectar funciones sensibles.

## Fase 3A — Dominio de configuración

Se agregaron en `PamAutomation.Core` los modelos y validadores de configuración no sensible: acción final, solicitud, fecha, hora, duración, razón, zona horaria y modo de prueba. La validación determinista permite aplicar las mismas reglas desde la interfaz y desde pruebas sin depender del sistema operativo.

## Fase 3B — Persistencia no sensible

Se definió `IAppSettingsStore` en Core y se implementó `JsonAppSettingsStore` en Infrastructure. La configuración se guarda con esquema explícito y sustitución atómica bajo `%LOCALAPPDATA%`. Esta fase evitó archivos `.env`, escrituras parciales y acoplamiento del dominio al sistema de archivos.

## Fase 4 — Credenciales PAM protegidas

Se definieron los modelos y el contrato de credenciales en Core y se implementó `WindowsPamCredentialStore` en Infrastructure mediante Windows Credential Manager. Usuario, contraseña y semilla TOTP se mantienen juntos, versionados y protegidos para el usuario actual. Las pruebas usan una API falsa, por lo que no leen ni modifican credenciales reales.

## Fase 5 — Generación TOTP

Se implementaron en `PamAutomation.Core/Totp` la decodificación Base32 y el algoritmo RFC 6238 con HMAC-SHA1, códigos de seis dígitos e intervalos de 30 segundos. `TotpCode` modela la ventana de validez, aplica el mínimo requerido de ocho segundos y redacta su representación textual. Las pruebas usan exclusivamente vectores públicos y cubren claves inválidas y límites temporales. Esto permite verificar el cálculo criptográfico antes de incorporar en la fase 6 la hora HTTP confiable del portal.

La fase 5 no accede a Credential Manager, Edge, BeyondInsight u Outlook. Fue validada desde la terminal en `Release | x64`: la solución compiló sin errores y las 71 pruebas terminaron correctamente.

## Fase 6 — Hora HTTP confiable

Se definieron en `PamAutomation.Core/Interfaces/Time` los contratos `IClock` e `IPamClockFactory` y los errores estables de calibración. `PamHttpDateClockFactory`, en `PamAutomation.Infrastructure/Time`, valida el encabezado HTTP `Date` en formato RFC 1123, estima el instante de recepción con la resolución del encabezado y la mitad del tiempo de ida y vuelta, y desde allí avanza únicamente con la fuente monotónica de `TimeProvider`. No existe respaldo silencioso al reloj local. Las pruebas deterministas cubren fechas ausentes o inválidas, mediciones negativas, desfase del portal y avance monotónico.

## Fase 7 — Edge y contexto Playwright restringido

Se incorporó Microsoft.Playwright 1.62.0 en `PamAutomation.Browser` y se implementaron los contratos de sesión definidos en Core. La fábrica lanza el Edge instalado mediante `Channel = "msedge"` en modo visible, crea un contexto temporal no persistente, limita la excepción HTTPS a ese contexto, bloquea service workers y aborta solicitudes HTTP(S) fuera del origen PAM exacto por esquema, host y puerto. La sesión no expone objetos Playwright ni cabeceras generales: solo devuelve URI final, estado y el encabezado `Date` necesario para la fase 6. Los dobles unitarios verifican opciones, restricción de origen, errores controlados, cancelación y liberación de recursos sin abrir Edge ni contactar PAM.

Las fases 6 y 7 fueron integradas y validadas conjuntamente en `Release | x64`: la solución compiló sin errores y las 90 pruebas terminaron correctamente. No se ejecutaron pruebas reales contra Edge o PAM porque requieren autorización explícita.

## Fase 8 — Autenticación PAM simulada

Se implementó en Core un servicio de autenticación aislado que enlaza el puerto del portal con la misma sesión temporal de navegador, calibra el reloj con HTTP `Date` y el tiempo monotónico de ida y vuelta, genera el TOTP y exige al menos ocho segundos de vigencia. El flujo simulado diferencia credenciales rechazadas, dos rechazos TOTP, fallos de comunicación, ausencia de confirmación y respuestas inesperadas. Solo reintenta una vez ante un rechazo TOTP explícito, espera que desaparezca el rechazo anterior y nunca utiliza credenciales reales, selectores Playwright ni el portal PAM. Las pruebas cubren todos esos caminos con dobles deterministas.

## Fase 9 — Autenticación PAM real visible

Se implementó en `PamAutomation.Browser` el adaptador Playwright real enlazado a la misma página y contexto temporal de `PamBrowserSession`. Los selectores accesibles observados se centralizaron, los resultados ambiguos no se clasifican como rechazo y el anuncio `Navigation complete.` se distingue del error de credenciales. Una prueba de integración opt-in lee exclusivamente Windows Credential Manager, abre Edge externo visible y limita su alcance al login. La validación autorizada confirmó `Return to dashboard` con el reloj HTTP y TOTP de las fases anteriores; después cerró la sesión sin navegar a Favorites, crear solicitudes o abrir Outlook. La solución compiló en `Release | x64` y ejecutó 127 pruebas correctas.

## Fase 10 — Navegación a Favorites

Se agregó un contrato de sesión autenticada reutilizable y `PamFavoritesService` para mantener login y lectura dentro del mismo contexto temporal. El adaptador real navega a `Accounts`, selecciona `Browse by Category > Favorites`, espera un grid estable, recorre el paginador accesible cuando existe y construye identidades redactadas `System + Application + Account` para cada `Favorites` row. Core rechaza resultados vacíos, incompletos o duplicados. La API de esta fase es exclusivamente de lectura y no permite seleccionar filas ni abrir formularios. La integración real autorizada confirmó `Favorites` y cerró Edge sin solicitar accesos. La solución compiló en `Release | x64` y ejecutó 146 pruebas correctas.

## Fase 11 — Modo de prueba de solicitudes

Se implementó `PamRequestTestService` y un contrato deliberadamente incapaz de enviar solicitudes. El flujo reutiliza la sesión autenticada de fase 10, recorre cada `Favorites` row, vuelve a comprobar `System + Application + Account`, abre `Access`, llena fecha, hora, duración y razón, valida `Application Session` y descarta el formulario mediante `Cancel > Discard Changes`. El adaptador instala además un bloqueo de red para `POST /api/ps-portal/requests`; cualquier intento interceptado hace fallar la prueba.

El resultado se mantiene en memoria, no contiene identidades y define `SubmittedCount = 0`. La integración real visible autorizada procesó 14 `Favorites` rows, validó y descartó los 14 formularios y confirmó cero envíos. La solución compiló en `Release | x64` y el proyecto completo ejecutó 154 pruebas correctas.

## Fase 12 — Solicitudes reales confirmadas

Se incorporó un flujo separado del modo de prueba para crear solicitudes reales. `PamRequestSubmissionService` valida la configuración, abre una única sesión autenticada, vuelve a leer todas las `Favorites` rows y exige una autorización ligada al conteo y a la configuración exactos antes de habilitar cualquier envío. Cada fila se vuelve a localizar por `System + Application + Account`, el formulario se llena y verifica inmediatamente antes de `Submit Request`, y solo una respuesta HTTP 2xx se clasifica como enviada.

El conflicto conocido `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se conserva como resultado controlado y permite continuar; cualquier otra respuesta detiene la ejecución sin exponer el contenido devuelto por PAM. Los resultados permanecen en memoria. La integración destructiva está excluida del flujo normal y requiere el opt-in exacto `PAM_AUTOMATION_ALLOW_SINGLE_REAL_REQUEST=CREATE_ONE_REQUEST_TODAY`.

Con autorización explícita del usuario se ejecutó una sola vez esa integración limitada a un intento. El 10 de septiembre de 2026 PAM aceptó una solicitud para la primera `Favorites` row, con inicio `08:20 AM` —normalizado como `08:20`— y duración de 5 minutos. Se guardaron capturas locales ignoradas por Git antes y después del envío; no se abrió Outlook ni se envió correo.

## Fase 13 — Lectura y comparación de Pending

Se agregó `PamPendingService` y un contrato de portal exclusivamente de lectura para navegar a `Requests`, seleccionar exactamente `Status = Pending` y recorrer hasta 500 páginas. Cada fila se convierte en un modelo redactado con Request ID numérico, `System`, `Application`, `Account`, `Request Start Date`, `Response`, `Expires` y `Description`; se rechazan filas incompletas, Request ID duplicados, estados distintos de `Pending`, paginación cambiante y asociaciones ambiguas.

La comparación localiza por fecha/hora completas más `System + Account` y valida por separado el `Application` solicitado, `Expires` calculado y `Description`. El portal puede representar `Application` como `nombre solicitado - destino`; solo se admite el nombre completo solicitado, con ese sufijo estructurado opcional, sin inferir familias desde `System`. Distingue coincidencias y faltantes sin sustituir filas, rechaza asociaciones múltiples y ofrece una espera acotada para la consistencia eventual. Todos los resultados permanecen en memoria.

La integración real visible confirmó primero el estado vacío y después filas reales autorizadas de 5 minutos. La validación final utilizó el 10 de septiembre de 2026 de `05:05 PM` a `05:10 PM`, comprobó una coincidencia ya existente sin crear una tercera solicitud y verificó `Request Start Date`, `Expires`, `Description`, `Application`, `System`, `Account` y `Response = Pending`. Las capturas locales muestran las columnas iniciales y finales mediante desplazamiento horizontal; están excluidas de Git.

## Fase 14 — Outlook falso y Outlook COM

Se definieron en Core los contratos, modelos redactados, validaciones y errores estables de Outlook. `PamAutomation.Outlook` incorpora Microsoft Office Interop Outlook 15.0.4797.1004, resolución exacta de cuenta por `SmtpAddress`, comprobación mediante mensaje temporal descartado, creación de un mensaje nuevo por envío, asignación previa de `SendUsingAccount`, ejecución en un hilo STA y liberación explícita de referencias COM. Las pruebas usan un seam COM falso: no abren Outlook ni envían correo. Como todavía no se consulta Enviados, un `Send()` correcto se informa honestamente como `SubmittedUnconfirmed`, no como entrega confirmada.

Las fases 8 y 14 fueron integradas y validadas conjuntamente en `Release | x64`: la solución compiló sin errores y las 116 pruebas terminaron correctamente. Permanece una advertencia MSTest heredada fuera de estas fases.

## Fase 15 — HTML saneado y marcador Request ID

Se incorporó `OutlookHtmlComposer` en Core como única ruta para preparar el cuerpo final del correo. Recibe XHTML generado por la aplicación, lo analiza con el parser XML de .NET y DTD deshabilitados, elimina elementos activos, atributos de eventos, estilos no permitidos y recursos externos, y conserva un subconjunto explícito de párrafos, formato de texto, listas, tablas e imágenes `cid:`. El límite de entrada es de 8 millones de caracteres y los errores no reproducen contenido privado.

La plantilla debe contener `{{REQUEST_IDS}}` exactamente una vez en un nodo de texto. La composición acepta únicamente Request ID numéricos y únicos, conserva su orden y sustituye el marcador por un ID por línea. `OutlookMessageValidator` vuelve a validar que el cuerpo sea idéntico a su representación saneada y contenga todos los identificadores antes de crear cualquier objeto COM, evitando que un consumidor omita accidentalmente el compositor.

Esta fase no implementa todavía el adaptador WPF desde `FlowDocument`, la persistencia protegida de la configuración de correo ni adjuntos CID; define y prueba el límite seguro que esos componentes deberán utilizar. No abrió PAM ni Outlook y no creó solicitudes ni correos. La solución compiló en `Release | x64` y la suite ordinaria ejecutó 225 pruebas correctas.

## Fase 16 — Resultados en memoria e interfaz

Se agregó `PamAutomationExecutionService` como coordinador de aplicación en Core. Una preparación real lee dinámicamente todas las `Favorites` rows y entrega a WPF un snapshot no sensible; el diálogo muestra conteo, fecha, hora, duración y razón. Solo después de una respuesta afirmativa se crea una autorización de un uso. El servicio real vuelve a comprobar el snapshot, procesa solicitudes y consulta `Pending` en una sesión autenticada para recuperar los Request ID exactos de las filas enviadas.

`MainWindowViewModel` carga la configuración no sensible y el estado de Windows Credential Manager, bloquea todas las acciones durante una ejecución y conecta comandos asíncronos separados para prueba y modo real. La aplicación permite configurar credenciales sin recuperar secretos y editar la solicitud con hora `AM/PM`; los resultados muestran conteos, conflictos, Request ID y hora final en memoria, además de un diálogo modal. Los fallos conocidos conservan mensajes controlados y cualquier excepción desconocida se presenta mediante un mensaje fijo sin detalles internos.

La configuración de correo se completa mediante un editor nativo `RichTextBox`/`FlowDocument` para texto con formato, listas, tablas e imágenes pegadas. El convertidor genera únicamente XHTML saneado; las imágenes se convierten a PNG, se persisten dentro de `mail-settings.dat` protegido con DPAPI `CurrentUser` y se materializan como adjuntos CID en una carpeta temporal única que se elimina inmediatamente después de agregarlos a Outlook.

Antes de guardar se valida la cuenta mediante un mensaje temporal descartado. La acción manual `Guardar y enviar prueba` exige confirmación y solo puede enviar desde y hacia la cuenta SMTP configurada. El correo del modo de prueba PAM exige otra confirmación inmediata, utiliza únicamente `tellezoe@hitss.com`, deja CC vacío, antepone `[PRUEBA]` y usa la lista simulada canónica. El modo real utiliza sus Request ID reconciliados.

La protección de correo combina una huella en memoria con una propiedad privada de Outlook que representa el asunto y el conjunto exacto de Request ID. Antes y después de enviar se consultan Enviados, Bandeja de salida y Borradores; un duplicado no se vuelve a crear y un mensaje idéntico en Bandeja de salida se mueve a Borradores. El resultado muestra el estado de entrega sin afirmar confirmación cuando Outlook no puede demostrarla. Los fallos parciales de PAM muestran cuántas `Favorites` rows alcanzaron un resultado conocido y advierten revisar `Pending` antes de reintentar.

La validación ordinaria compiló en `Release | x64`, ejecutó 243 pruebas correctas y arrancó la ventana WPF sin cierre inesperado. No se abrió PAM, no se envió correo y no se consumió ninguna de las diez pruebas reales autorizadas.

## Fases pendientes

La clasificación siguiente se refiere únicamente a dependencias entre fases pendientes:

- **Independiente:** no necesita que termine otra fase pendiente y puede desarrollarse en paralelo en módulos separados.
- **Independiente condicionada:** puede avanzar en paralelo solo después de que el coordinador fije el contrato compartido, asigne archivos sin solapamiento y limite el alcance indicado.
- **Dependiente:** necesita resultados integrados de las fases señaladas o comparte una superficie de cambio que hace más segura la ejecución secuencial.

| Fase | Clasificación | Dependencias pendientes | Alcance y posibilidad de paralelismo |
|---|---|---|---|
| 17 — Evidencias técnicas de desarrollo | **Dependiente** | Fase 16 y flujos operativos integrados. | Es transversal y debe conocer qué fallos pueden diagnosticarse sin persistir secretos. No conviene paralelizarla con cambios activos en esos flujos. |
| 18 — Instalador y publicación autocontenida | **Dependiente** | Fases 6 a 17. | Requiere que la aplicación y sus dependencias externas estén definidas y validadas. |
| 19 — Pruebas finales administradas | **Dependiente** | Fase 18. | Es la validación final del producto publicado en el entorno Windows objetivo. |
| 20 — Colecciones personalizadas post-migración | **Dependiente** | Migración inicial y fase 19 completadas. | Permitirá seleccionar subconjuntos editables de `Favorites` rows, guardar configuración general y posibles sobrescrituras por fila. Es alcance nuevo y no debe mezclarse con las fases 12 a 19. |

## Mapa recomendado de trabajo paralelo

```text
Fases 6 a 16 completadas ──► Fase 17 ──► Fase 18 ──► Fase 19

Fase 19 ──► Fase 20 (ampliación posterior a la migración)
```

La fase 17 constituye el siguiente paso y depende de los flujos integrados en fase 16. El coordinador solo podrá paralelizar una parte futura si vuelve a demostrar independencia, contratos congelados y ausencia de archivos compartidos.

La fase 20 registra una ampliación solicitada para después de terminar la migración: colecciones locales nombradas y editables que seleccionan cualquier subconjunto de Favorites. Su diseño preliminar, navegación, reconciliación y seguridad se detallan en `PAM_NAVIGATION.md`; no modifica el alcance ni la autorización de la fase 12.

Esta clasificación es una guía de planificación, no una autorización automática para crear subagentes. Antes de cada oleada, el coordinador debe inspeccionar el diff y la estructura real; si el trabajo resultó pequeño o apareció solapamiento, debe conservarlo en el agente principal o ejecutarlo secuencialmente.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
