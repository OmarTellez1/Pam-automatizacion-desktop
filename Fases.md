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

## Fases pendientes

La clasificación siguiente se refiere únicamente a dependencias entre fases pendientes:

- **Independiente:** no necesita que termine otra fase pendiente y puede desarrollarse en paralelo en módulos separados.
- **Independiente condicionada:** puede avanzar en paralelo solo después de que el coordinador fije el contrato compartido, asigne archivos sin solapamiento y limite el alcance indicado.
- **Dependiente:** necesita resultados integrados de las fases señaladas o comparte una superficie de cambio que hace más segura la ejecución secuencial.

| Fase | Clasificación | Dependencias pendientes | Alcance y posibilidad de paralelismo |
|---|---|---|---|
| 8 — Autenticación PAM simulada | **Independiente condicionada** | Ninguna fase pendiente; fases 6 y 7 completadas. | Ya puede comenzar sobre los contratos integrados de hora y navegador. Puede avanzar en paralelo con la fase 14 si se congelan esos contratos y no se comparten archivos. |
| 9 — Autenticación PAM real visible | **Dependiente** | Fase 8 y autorización explícita del usuario. | Es una validación real y secuencial; no se delega en paralelo con operaciones reales de PAM. |
| 10 — Navegación a Favorites | **Dependiente** | Fase 9. | Necesita una sesión autenticada confirmada y los selectores reales inspeccionados. |
| 11 — Modo de prueba de solicitudes | **Dependiente** | Fase 10. | Necesita las cuentas descubiertas y el flujo de navegación estable; valida y descarta formularios sin enviarlos. |
| 12 — Solicitudes reales | **Dependiente** | Fase 11 y confirmación explícita. | Reutiliza el mismo flujo y archivos de Browser; debe implementarse después de demostrar que el modo de prueba nunca envía solicitudes. |
| 13 — Lectura y comparación de Pending | **Dependiente** | Fases 10, 11 y 12. | Aunque conceptualmente es lectura, comparte sesión, selectores y modelos de solicitudes con las fases anteriores; mantenerla secuencial evita conflictos en Browser. |
| 14 — Outlook falso y Outlook COM | **Independiente** | Ninguna fase pendiente. | Es el segundo frente seguro de trabajo: puede desarrollarse en `PamAutomation.Outlook` y sus pruebas mientras otro agente trabaja en una fase PAM, sin enviar correos reales ni tocar Browser. |
| 15 — HTML saneado y marcador Request ID | **Dependiente** | Fases 13 y 14. | Necesita el contrato final de resultados PAM y la abstracción de correo. Puede adelantarse solo si el coordinador congela ambos contratos, por lo que no se considera independiente por defecto. |
| 16 — Resultados en memoria e interfaz | **Dependiente** | Fases 13 y 15. | Necesita los resultados y estados finales que debe presentar; probablemente afecta App y la composición principal. |
| 17 — Evidencias técnicas de desarrollo | **Dependiente** | Fase 16 y flujos operativos integrados. | Es transversal y debe conocer qué fallos pueden diagnosticarse sin persistir secretos. No conviene paralelizarla con cambios activos en esos flujos. |
| 18 — Instalador y publicación autocontenida | **Dependiente** | Fases 6 a 17. | Requiere que la aplicación y sus dependencias externas estén definidas y validadas. |
| 19 — Pruebas finales administradas | **Dependiente** | Fase 18. | Es la validación final del producto publicado en el entorno Windows objetivo. |

## Mapa recomendado de trabajo paralelo

```text
Fases 6 y 7 completadas
       │
       └──► Fase 8 ──► Fase 9 ──► Fase 10 ──► Fase 11 ──► Fase 12 ──► Fase 13

Fase 14 ────────────────────────────────────────────────────────────────────────────────┐
                                                                                        ├──► Fase 15 ──► Fase 16 ──► Fase 17 ──► Fase 18 ──► Fase 19
Fase 13 ────────────────────────────────────────────────────────────────────────────────┘
```

Con un máximo de dos subagentes, las combinaciones recomendadas son:

1. **Siguiente oleada posible:** fase 8 y fase 14, porque autenticación simulada y Outlook pertenecen a módulos separados. Si alguna es pequeña en el estado real del repositorio, el coordinador debe asumirla directamente y usar solo un subagente.
2. **Resto del flujo:** ejecutar de forma predominantemente secuencial. El coordinador solo podrá paralelizar una parte si vuelve a comprobar independencia, contratos congelados y ausencia de archivos compartidos.

Esta clasificación es una guía de planificación, no una autorización automática para crear subagentes. Antes de cada oleada, el coordinador debe inspeccionar el diff y la estructura real; si el trabajo resultó pequeño o apareció solapamiento, debe conservarlo en el agente principal o ejecutarlo secuencialmente.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
