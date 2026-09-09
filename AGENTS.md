# AGENTS.md — Pam Automation .NET

## Alcance

Este repositorio es una aplicación nueva construida desde cero para Windows. La aplicación anterior está en C:\pam automatizacion y solo es referencia de lectura.

La raíz oficial del repositorio es `C:\pam automatizacion dotnet\PamAutomation` y la solución utiliza el formato moderno `PamAutomation.slnx`.

Antes de analizar, modificar o crear código, leer completamente:

- CODEX_BUILD_SPEC.md
- README.md
- LOGIC.md
- History.md
- structure.md
- Fases.md

Debes mantener actualizado structure.md, este markdown se trata de documentar la estructura del proyecto e indicar que hace cada paquete, subpaquete y modulos.
Manten siempre actualizado Fases.md con todas las fases que se ha desarrollado en el proyecto, explicando que incorporo al desarrollo y en donde y por que fue importante implementar esa fase, tambien en ese markdown describe las fases completadas y las fases pendientes por hacer y completar.
## Tecnología obligatoria

- C# y .NET 10 LTS, salvo restricción corporativa documentada.
- WPF y XAML para la interfaz.
- Microsoft.Playwright para .NET.
- Microsoft Edge instalado externamente mediante el canal msedge, usando un contexto temporal de Playwright sin reutilizar perfiles existentes.
- Outlook clásico mediante Microsoft Office Interop/COM.

No utilizar JavaScript, TypeScript, Node.js, Python, Java, PowerShell, Electron, WebView2, Selenium ni Chromium incluido en la aplicación.

HTML, CSS y JavaScript no forman parte de la interfaz propia. Playwright puede inspeccionar el DOM HTML del portal PAM externo porque ese portal es una aplicación web.

## Reglas de trabajo

- Construir por fases pequeñas y revisables.
- No copiar la arquitectura del proyecto anterior.
- No modificar C:\pam automatizacion.
- No utilizar LaunchPersistentContext, userDataDir ni conexión CDP a un perfil existente.
- Las pruebas de integración autorizadas pueden leer credenciales reales desde el almacén protegido; nunca imprimirlas, registrarlas, hardcodearlas ni almacenarlas en texto plano.
- No crear solicitudes PAM ni enviar correos durante pruebas.
- No usar credenciales reales en pruebas unitarias o simulaciones. Pueden utilizarse en pruebas de integración visibles y explícitamente autorizadas contra el entorno PAM permitido.
- Ejecutar compilación y pruebas relacionadas después de cada cambio.
- No afirmar que una tarea está terminada sin evidencia.
- Si documentación, código o portal presentan contradicciones, detenerse y reportarlas.
- Mantener actualizados README.md, LOGIC.md y History.md tras decisiones o cambios relevantes.

## Seguridad

- Usar Windows Credential Manager o DPAPI.
- No guardar contraseñas ni semillas TOTP en texto plano.
- No escribir TOTP, cookies, tokens ni secretos en logs o capturas.
- Permitir navegación únicamente al origen autorizado de PAM.
- Mantener la excepción HTTPS limitada al contexto de PAM.
- No utilizar archivos .env para configuración o secretos.
- Guardar la contraseña y la semilla TOTP en Windows Credential Manager.
- Guardar la configuración privada bajo %LOCALAPPDATA%\PamAutomation\config.
- El ejecutable publicado no debe crear ni mantener resultados, logs, artefactos o temporales operativos persistentes en la PC del usuario.
- Durante desarrollo y pruebas, sí se permite crear `results`, `logs`, `artifacts` y `temp` en la raíz del repositorio para analizar fallos; deben estar excluidos de Git y no contener secretos.
- Proteger con DPAPI CurrentUser el contenido de correo que pueda contener información sensible.
- No escribir configuración dentro de Program Files ni junto al ejecutable.
- Mantener los resultados de ejecución solamente en memoria, salvo una exportación de diagnóstico solicitada explícitamente.
- Usar escrituras atómicas y schemaVersion para permitir migraciones de configuración.

## Validación

La restauración, compilación, ejecución, pruebas y publicación se realizarán desde la terminal con el SDK x64 de .NET 10 disponible en `PATH`. Usar directamente `dotnet restore`, `dotnet build`, el runner integrado de MSTest mediante `dotnet run` y `dotnet publish`; no crear scripts auxiliares para envolver estos comandos. La configuración canónica de desarrollo es `Release | x64`. Las pruebas reales contra PAM y Outlook requieren autorización explícita.
