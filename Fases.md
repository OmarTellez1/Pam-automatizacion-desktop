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

Se agregó un contrato de sesión autenticada reutilizable y `PamFavoritesService` para mantener login y lectura dentro del mismo contexto temporal. El adaptador real navega a Accounts, selecciona `Browse by Category > Favorites`, espera un grid estable, recorre el paginador accesible cuando existe y construye identidades redactadas de sistema, aplicación y cuenta. Core rechaza resultados vacíos, incompletos o duplicados. La API de esta fase es exclusivamente de lectura y no permite seleccionar filas ni abrir formularios. La integración real autorizada confirmó Favorites y cerró Edge sin solicitar accesos. La solución compiló en `Release | x64` y ejecutó 146 pruebas correctas.

## Fase 14 — Outlook falso y Outlook COM

Se definieron en Core los contratos, modelos redactados, validaciones y errores estables de Outlook. `PamAutomation.Outlook` incorpora Microsoft Office Interop Outlook 15.0.4797.1004, resolución exacta de cuenta por `SmtpAddress`, comprobación mediante mensaje temporal descartado, creación de un mensaje nuevo por envío, asignación previa de `SendUsingAccount`, ejecución en un hilo STA y liberación explícita de referencias COM. Las pruebas usan un seam COM falso: no abren Outlook ni envían correo. Como todavía no se consulta Enviados, un `Send()` correcto se informa honestamente como `SubmittedUnconfirmed`, no como entrega confirmada.

Las fases 8 y 14 fueron integradas y validadas conjuntamente en `Release | x64`: la solución compiló sin errores y las 116 pruebas terminaron correctamente. Permanece una advertencia MSTest heredada fuera de estas fases.

## Fases pendientes

La clasificación siguiente se refiere únicamente a dependencias entre fases pendientes:

- **Independiente:** no necesita que termine otra fase pendiente y puede desarrollarse en paralelo en módulos separados.
- **Independiente condicionada:** puede avanzar en paralelo solo después de que el coordinador fije el contrato compartido, asigne archivos sin solapamiento y limite el alcance indicado.
- **Dependiente:** necesita resultados integrados de las fases señaladas o comparte una superficie de cambio que hace más segura la ejecución secuencial.

| Fase | Clasificación | Dependencias pendientes | Alcance y posibilidad de paralelismo |
|---|---|---|---|
| 11 — Modo de prueba de solicitudes | **Dependiente** | Autorización explícita para formularios reales; fase 10 completada. | Ya dispone de las cuentas descubiertas y el flujo de navegación estable; debe validar y descartar cada formulario demostrando que nunca pulsa `Submit Request`. |
| 12 — Solicitudes reales | **Dependiente** | Fase 11 y confirmación explícita. | Reutiliza el mismo flujo y archivos de Browser; debe implementarse después de demostrar que el modo de prueba nunca envía solicitudes. |
| 13 — Lectura y comparación de Pending | **Dependiente** | Fases 10, 11 y 12. | Aunque conceptualmente es lectura, comparte sesión, selectores y modelos de solicitudes con las fases anteriores; mantenerla secuencial evita conflictos en Browser. |
| 15 — HTML saneado y marcador Request ID | **Dependiente** | Fase 13; fase 14 completada. | Ya dispone de la abstracción de Outlook, pero aún necesita el contrato final de resultados PAM para componer y validar los Request ID. |
| 16 — Resultados en memoria e interfaz | **Dependiente** | Fases 13 y 15. | Necesita los resultados y estados finales que debe presentar; probablemente afecta App y la composición principal. |
| 17 — Evidencias técnicas de desarrollo | **Dependiente** | Fase 16 y flujos operativos integrados. | Es transversal y debe conocer qué fallos pueden diagnosticarse sin persistir secretos. No conviene paralelizarla con cambios activos en esos flujos. |
| 18 — Instalador y publicación autocontenida | **Dependiente** | Fases 6 a 17. | Requiere que la aplicación y sus dependencias externas estén definidas y validadas. |
| 19 — Pruebas finales administradas | **Dependiente** | Fase 18. | Es la validación final del producto publicado en el entorno Windows objetivo. |

## Mapa recomendado de trabajo paralelo

```text
Fases 6 a 10 completadas
       │
       └──► Fase 11 ──► Fase 12 ──► Fase 13

Fase 14 completada ─────────────────────────────────────────────────────────────────────┐
                                                                                        ├──► Fase 15 ──► Fase 16 ──► Fase 17 ──► Fase 18 ──► Fase 19
Fase 13 ────────────────────────────────────────────────────────────────────────────────┘
```

No queda actualmente otra fase clasificada como independiente. El flujo PAM debe continuar de forma secuencial desde la fase 11 y la fase 15 seguirá bloqueada hasta completar la fase 13. El coordinador solo podrá paralelizar una parte futura si vuelve a demostrar independencia, contratos congelados y ausencia de archivos compartidos.

Esta clasificación es una guía de planificación, no una autorización automática para crear subagentes. Antes de cada oleada, el coordinador debe inspeccionar el diff y la estructura real; si el trabajo resultó pequeño o apareció solapamiento, debe conservarlo en el agente principal o ejecutarlo secuencialmente.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
