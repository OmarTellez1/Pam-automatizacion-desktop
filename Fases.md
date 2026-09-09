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

## Fases pendientes

- Fase 6: abstraer y validar la hora HTTP confiable del portal, sin recurrir al reloj local.
- Fase 7: lanzar Edge instalado mediante Playwright .NET con un contexto temporal y origen restringido.
- Fase 8: implementar y probar la autenticación PAM simulada, incluidos rechazo y reintento único del TOTP.
- Fase 9: validar la autenticación real visible con autorización explícita y credenciales protegidas.
- Fase 10: navegar a Favorites y descubrir cuentas dinámicamente.
- Fase 11: implementar el modo de prueba que valida y descarta formularios sin enviar solicitudes.
- Fase 12: implementar solicitudes reales detrás de confirmación explícita.
- Fase 13: leer Pending con paginación, comparación y manejo de conflictos.
- Fase 14: implementar primero Outlook falso y después Outlook clásico mediante COM.
- Fase 15: componer HTML saneado y sustituir el marcador de Request ID.
- Fase 16: mostrar y conservar resultados únicamente en memoria durante la ejecución.
- Fase 17: habilitar evidencias técnicas exclusivas de desarrollo, excluidas de Git y sin secretos.
- Fase 18: crear el instalador y la publicación autocontenida sin evidencias de desarrollo.
- Fase 19: ejecutar las pruebas finales desde la terminal en un equipo Windows administrado.

Cada fase deberá restaurar dependencias cuando corresponda, compilar en `Release | x64` y ejecutar sus pruebas relacionadas desde la terminal antes de considerarse completa.
