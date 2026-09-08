# Historial — Pam Automation .NET

## 8 de septiembre de 2026 — Inicio documental

- Se decidió construir una aplicación nueva desde cero exclusivamente con C# y .NET.
- La aplicación objetivo será Windows x64 con WPF/XAML.
- La automatización web utilizará Microsoft.Playwright con Microsoft Edge instalado externamente.
- No se incluirán Chromium, Node.js, JavaScript, Python, PowerShell, Electron, Selenium ni WebView2.
- Outlook clásico se integrará mediante Microsoft Office Interop/COM.
- La aplicación anterior en C:\pam automatizacion queda como referencia funcional y visual de solo lectura.
- Se creó la documentación inicial propia del nuevo proyecto.
- Todavía no existe código fuente, solución .NET, instalador ni prueba de integración.
- No se han usado credenciales reales, no se han creado solicitudes PAM y no se han enviado correos desde este proyecto.

## Decisiones pendientes

- Confirmar si la API oficial de BeyondTrust puede cubrir alguna parte del flujo sin sustituir Playwright.
- Elegir MSTest o xUnit.
- Validar el editor nativo de contenido enriquecido para correo sin JavaScript.
- Definir MSI o MSIX como formato final de distribución.
- Confirmar políticas corporativas de Edge y Outlook clásico.

## 8 de septiembre de 2026 — Correcciones de pruebas, perfil y distribución

- Se autorizó el uso de credenciales reales únicamente en pruebas de integración visibles y explícitamente autorizadas; las pruebas unitarias y simulaciones seguirán usando datos falsos.
- Se decidió utilizar Edge instalado con un contexto temporal de Playwright, sin seleccionar ni reutilizar perfiles existentes.
- La aplicación no debe conectarse a un Edge ya abierto ni modificar perfiles, cookies, extensiones o sesiones del usuario.
- Se explicitó que el entregable final será un ejecutable Windows x64 autocontenido que no requerirá .NET, Node.js, Python ni PowerShell instalados.
- Edge, Outlook clásico, Windows y la conectividad al portal PAM permanecen como requisitos externos deliberados.

## 8 de septiembre de 2026 — Decisión sobre perfiles del navegador

- Se eligió la opción 1 para Playwright.
- La aplicación lanzará el Edge instalado y utilizará un contexto temporal de Playwright.
- No seleccionará ni reutilizará perfiles existentes, cookies, extensiones o sesiones del usuario.
- No utilizará LaunchPersistentContext, userDataDir ni conexión CDP a un navegador existente.
- El contexto temporal no debe mostrarse como una ventana de incógnito; el aislamiento será interno de Playwright.

## 8 de septiembre de 2026 — Persistencia local y credenciales

- Se decidió no utilizar archivos .env en la aplicación nueva.
- Las credenciales PAM se almacenarán en Windows Credential Manager por usuario de Windows.
- La configuración general se guardará en app-settings.json bajo %LOCALAPPDATA%\PamAutomation.
- La configuración de correo se guardará en mail-settings.dat y se protegerá con DPAPI CurrentUser.
- La configuración privada permanecerá bajo %LOCALAPPDATA%\PamAutomation\config.
- Se descartó guardar resultados, logs, artefactos y temporales operativos en la PC del usuario. Los resultados vivirán en memoria durante la ejecución y no habrá auditoría local por defecto.
- La configuración utilizará schemaVersion y escrituras atómicas.

## 8 de septiembre de 2026 — Instalación de Visual Studio

- Para desarrollar la aplicación se utilizará la carga de trabajo Desarrollo de escritorio de .NET.
- No se requieren .NET MAUI, WinUI, C++, desarrollo móvil, ASP.NET, Node.js, Python, Java ni componentes opcionales como Blend o Copilot.
- La solución se desarrollará con WPF, XAML, C# y .NET 10 LTS.

## 8 de septiembre de 2026 — Eliminación de archivos operativos persistentes

- La estructura persistente se limitará a `%LOCALAPPDATA%\PamAutomation\config\app-settings.json` y `mail-settings.dat`.
- Las credenciales permanecerán en Windows Credential Manager y la configuración de correo seguirá protegida con DPAPI CurrentUser.
- No se crearán carpetas persistentes de `results`, `logs`, `artifacts` o `temp`.
- Request ID, conflictos y estados se conservarán únicamente en memoria durante la ejecución.
- El debugging o diagnóstico requerirá una activación y exportación explícitas por parte del desarrollador o soporte.

## 8 de septiembre de 2026 — Separación entre desarrollo y publicación

- La aplicación publicada no persistirá logs, resultados, artefactos ni temporales en la PC de los usuarios.
- El repositorio de desarrollo sí podrá crear `results`, `logs`, `artifacts` y `temp` en su raíz para facilitar el análisis de fallos.
- Esas carpetas serán exclusivas de desarrollo, estarán excluidas de Git, no se incluirán en el instalador y no contendrán secretos.

## Regla histórica

Cada cambio relevante debe registrar qué se decidió, por qué, qué se verificó y qué queda pendiente.
