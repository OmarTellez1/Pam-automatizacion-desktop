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
- Validar mediante un prototipo el editor nativo de contenido enriquecido y su conversión limitada a HTML para Outlook.
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

## 8 de septiembre de 2026 — Alineación de la solución y decisiones iniciales

- Se confirmó que la raíz oficial es `C:\pam automatizacion dotnet\PamAutomation`.
- Se confirmó que la solución existente utiliza `PamAutomation.slnx`; no se requiere convertirla a `.sln`.
- Se actualizó el estado documental: ya existe `PamAutomation.App`, un proyecto WPF mínimo para `net10.0-windows`; todavía faltan los demás proyectos y toda la implementación funcional.
- Se estableció que la compilación y las pruebas se operarán desde Visual Studio, sin depender de `dotnet` o `msbuild` en `PATH`.
- Se eligió MSTest con Microsoft Testing Platform por su integración directa con Visual Studio y su soporte para pruebas STA necesarias alrededor de Outlook COM.
- Se definió `testModeEnabled`, falso por defecto, como opción no sensible para habilitar el botón de prueba. El usuario podrá cambiarla desde una sección avanzada de Configuración de solicitud y el botón requerirá configuración válida.
- Se autorizó el envío manual de correos del modo de prueba únicamente a `tellezoe@hitss.com`, sin CC, con confirmación explícita y asunto `[PRUEBA]`.
- Se decidió conservar las reglas funcionales comprobadas del flujo anterior, pero utilizar Edge visible y un contexto temporal en la primera versión; no se reutilizarán perfiles y no se habilitará headless inicialmente.
- Se decidió evitar persistencia local de resultados y huellas de correo. La protección contra duplicados combinará estado en memoria con consulta de Enviados, Bandeja de salida y Borradores de Outlook.
- Se seleccionó `RichTextBox`/`FlowDocument` con un convertidor C# limitado a HTML saneado como dirección del editor. La viabilidad de tablas, imágenes CID y fidelidad de ida y vuelta sigue pendiente de un prototipo antes de implementar el editor completo.
- En esta actualización solo se modificó documentación; no se cambió código, no se compiló, no se accedió a PAM y no se abrió ni utilizó Outlook.

## 8 de septiembre de 2026 — Fase 1: estructura base

- Se agregaron a `PamAutomation.slnx` los proyectos Core, Browser, Outlook, Infrastructure y Tests junto al proyecto WPF existente.
- App referencia Browser, Core, Infrastructure y Outlook. Browser, Infrastructure y Outlook referencian únicamente Core. Tests referencia inicialmente Core.
- Se configuró MSTest SDK 4.1.0 con Microsoft Testing Platform y una prueba mínima de descubrimiento para Test Explorer.
- Se eliminaron `using` y espacios vacíos innecesarios de la plantilla WPF, sin agregar comportamiento funcional.
- No se agregaron Playwright, Outlook Interop, Credential Manager, DPAPI, TOTP ni lógica PAM.
- La compilación Release y la ejecución de la prueba quedan pendientes en Visual Studio porque esta sesión no expuso acceso a aplicaciones nativas de Windows y las reglas del proyecto prohíben sustituir el IDE con comandos de terminal.
- No se accedió a PAM, no se utilizaron credenciales reales, no se crearon solicitudes y no se enviaron correos.

## Regla histórica

Cada cambio relevante debe registrar qué se decidió, por qué, qué se verificó y qué queda pendiente.

## 8 de septiembre de 2026 — Fase 2: presentación WPF y MVVM

- Se reemplazó la ventana WPF vacía por la pantalla principal de configuración, acción final, estado y acciones de prueba y ejecución.
- Se agregó `CommunityToolkit.Mvvm` 8.4.2 para mantener propiedades observables y comandos fuera del code-behind.
- Se centralizaron colores y estilos XAML para facilitar consistencia, escalado y mantenimiento.
- La aplicación inicia con configuración incompleta; la automatización real permanece deshabilitada hasta cumplir sus requisitos y el botón de prueba requiere modo de prueba y configuración válida.
- Se agregaron pruebas unitarias para el estado inicial seguro, el bloqueo durante una ejecución y las condiciones de visibilidad del modo de prueba.
- Se verificó estáticamente que los archivos XAML son XML válido y que el diff no contiene errores de espacios. La restauración de paquetes, compilación y ejecución de pruebas quedan pendientes en Visual Studio.
- Esta fase no agregó persistencia, credenciales, Playwright, acceso a PAM, Outlook ni envío de correo; no produjo efectos externos.

## 8 de septiembre de 2026 — Fase 2B: alineación visual con la aplicación anterior

- El usuario confirmó que la fase 2 compiló, pasó sus comprobaciones y pudo visualizarse correctamente en Visual Studio.
- Se realineó la portada con las capturas de la aplicación anterior: encabezado `Accesos PAM`, marca con la letra P, fondo claro con degradado, selector Outlook superior, acción azul principal centrada y tres accesos horizontales de configuración.
- Se conservaron el ViewModel, las condiciones seguras de habilitación, el botón de prueba condicionado y las pruebas de presentación existentes.
- Se aclaró que existen dos acciones diferentes: la verificación manual de Outlook puede enviar a la misma cuenta configurada; el correo de resultados del modo de prueba PAM continúa limitado a `tellezoe@hitss.com`.
- El rediseño utiliza únicamente WPF/XAML y recursos nativos; no se copió código, HTML, CSS o JavaScript de la aplicación anterior.
- Se comprobó estáticamente la estructura XML de los XAML. La compilación, las pruebas y la revisión visual del nuevo diseño quedan pendientes en Visual Studio.
- No se accedió a PAM, no se utilizaron credenciales, no se crearon solicitudes y no se envió correo.
