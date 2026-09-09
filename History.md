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

## 8 de septiembre de 2026 — Entorno de desarrollo inicial

- Para desarrollar la aplicación se utilizarán las herramientas de escritorio de .NET.
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
- Inicialmente se estableció un flujo de compilación y pruebas dependiente de un entorno gráfico; esta decisión fue sustituida posteriormente por el uso exclusivo de la terminal y la CLI `dotnet`.
- Se eligió MSTest con Microsoft Testing Platform por su runner integrado y su soporte para pruebas STA necesarias alrededor de Outlook COM.
- Se definió `testModeEnabled`, falso por defecto, como opción no sensible para habilitar el botón de prueba. El usuario podrá cambiarla desde una sección avanzada de Configuración de solicitud y el botón requerirá configuración válida.
- Se autorizó el envío manual de correos del modo de prueba únicamente a `tellezoe@hitss.com`, sin CC, con confirmación explícita y asunto `[PRUEBA]`.
- Se decidió conservar las reglas funcionales comprobadas del flujo anterior, pero utilizar Edge visible y un contexto temporal en la primera versión; no se reutilizarán perfiles y no se habilitará headless inicialmente.
- Se decidió evitar persistencia local de resultados y huellas de correo. La protección contra duplicados combinará estado en memoria con consulta de Enviados, Bandeja de salida y Borradores de Outlook.
- Se seleccionó `RichTextBox`/`FlowDocument` con un convertidor C# limitado a HTML saneado como dirección del editor. La viabilidad de tablas, imágenes CID y fidelidad de ida y vuelta sigue pendiente de un prototipo antes de implementar el editor completo.
- En esta actualización solo se modificó documentación; no se cambió código, no se compiló, no se accedió a PAM y no se abrió ni utilizó Outlook.

## 8 de septiembre de 2026 — Fase 1: estructura base

- Se agregaron a `PamAutomation.slnx` los proyectos Core, Browser, Outlook, Infrastructure y Tests junto al proyecto WPF existente.
- App referencia Browser, Core, Infrastructure y Outlook. Browser, Infrastructure y Outlook referencian únicamente Core. Tests referencia inicialmente Core.
- Se configuró MSTest SDK 4.1.0 con Microsoft Testing Platform y una prueba mínima de descubrimiento del runner.
- Se eliminaron `using` y espacios vacíos innecesarios de la plantilla WPF, sin agregar comportamiento funcional.
- No se agregaron Playwright, Outlook Interop, Credential Manager, DPAPI, TOTP ni lógica PAM.
- La compilación Release y la ejecución de la prueba quedaron pendientes en ese momento porque el flujo inicial todavía no autorizaba comandos de terminal.
- No se accedió a PAM, no se utilizaron credenciales reales, no se crearon solicitudes y no se enviaron correos.

## Regla histórica

Cada cambio relevante debe registrar qué se decidió, por qué, qué se verificó y qué queda pendiente.

## 8 de septiembre de 2026 — Fase 2: presentación WPF y MVVM

- Se reemplazó la ventana WPF vacía por la pantalla principal de configuración, acción final, estado y acciones de prueba y ejecución.
- Se agregó `CommunityToolkit.Mvvm` 8.4.2 para mantener propiedades observables y comandos fuera del code-behind.
- Se centralizaron colores y estilos XAML para facilitar consistencia, escalado y mantenimiento.
- La aplicación inicia con configuración incompleta; la automatización real permanece deshabilitada hasta cumplir sus requisitos y el botón de prueba requiere modo de prueba y configuración válida.
- Se agregaron pruebas unitarias para el estado inicial seguro, el bloqueo durante una ejecución y las condiciones de visibilidad del modo de prueba.
- Se verificó estáticamente que los archivos XAML son XML válido y que el diff no contiene errores de espacios. La restauración de paquetes, compilación y ejecución de pruebas quedaron pendientes en ese momento.
- Esta fase no agregó persistencia, credenciales, Playwright, acceso a PAM, Outlook ni envío de correo; no produjo efectos externos.

## 8 de septiembre de 2026 — Fase 2B: alineación visual con la aplicación anterior

- El usuario confirmó que la fase 2 compiló, pasó sus comprobaciones y pudo visualizarse correctamente.
- Se realineó la portada con las capturas de la aplicación anterior: encabezado `Accesos PAM`, marca con la letra P, fondo claro con degradado, selector Outlook superior, acción azul principal centrada y tres accesos horizontales de configuración.
- Se conservaron el ViewModel, las condiciones seguras de habilitación, el botón de prueba condicionado y las pruebas de presentación existentes.
- Se aclaró que existen dos acciones diferentes: la verificación manual de Outlook puede enviar a la misma cuenta configurada; el correo de resultados del modo de prueba PAM continúa limitado a `tellezoe@hitss.com`.
- El rediseño utiliza únicamente WPF/XAML y recursos nativos; no se copió código, HTML, CSS o JavaScript de la aplicación anterior.
- Se comprobó estáticamente la estructura XML de los XAML. La compilación, las pruebas y la revisión visual del nuevo diseño quedaron pendientes en ese momento.
- No se accedió a PAM, no se utilizaron credenciales, no se crearon solicitudes y no se envió correo.

## 8 de septiembre de 2026 — Documentación de estructura

- Se creó `structure.md` como inventario de la estructura física actual y referencia de la arquitectura objetivo.
- Se documentaron las responsabilidades y límites de App, Core, Browser, Infrastructure, Outlook y Tests.
- Se estableció la dirección permitida de dependencias y se aclaró que actualmente solo App y Tests contienen implementación.
- Se documentó una estructura final orientativa que deberá materializarse gradualmente, sin crear carpetas vacías por adelantado.
- Este cambio fue únicamente documental; no se modificó código, proyectos ni referencias de la solución.

## 9 de septiembre de 2026 — Fase 3A: dominio de configuración no sensible

- El usuario confirmó que la fase 2B compiló y se visualizó correctamente con `Release | x64`; la composición fue aprobada, pero sus elementos se perciben demasiado grandes y su escala queda pendiente de ajuste.
- Se agregaron en Core `AppSettings`, `RequestSettings` y `FinalAction`, sin dependencias de WPF o Windows.
- Los valores predeterminados mantienen `schemaVersion = 1`, `America/Guayaquil`, visualización de IDs y modo de prueba deshabilitado.
- Se agregaron validadores para esquema, acción final, zona horaria, fecha, hora, duración de 1 a 8 horas y razón obligatoria.
- La hora actual de Ecuador se recibe como entrada del validador para permitir pruebas deterministas sin implementar todavía un reloj del sistema.
- Se agregaron pruebas MSTest unitarias para escenarios válidos, nulos, límites y múltiples errores simultáneos.
- No se implementó persistencia, interfaz de configuración, Credential Manager, DPAPI, TOTP, Edge, PAM u Outlook.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 3B: persistencia JSON no sensible

- El usuario confirmó que la fase 3A compiló y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregó `IAppSettingsStore` en Core para separar el dominio del almacenamiento físico.
- Se implementó `JsonAppSettingsStore` en Infrastructure con la ruta predeterminada `%LOCALAPPDATA%\PamAutomation\config\app-settings.json`.
- El formato persistido es plano, versionado y estricto; utiliza representaciones ISO para `DateOnly` y `TimeOnly` y rechaza JSON inválido, propiedades desconocidas, valores faltantes y esquemas incompatibles.
- Un archivo inexistente devuelve valores predeterminados seguros sin crear almacenamiento. Guardar crea la carpeta cuando se necesita y sustituye el archivo mediante un temporal único en la misma ubicación.
- Se agregaron pruebas MSTest aisladas para valores predeterminados, formato JSON, ida y vuelta, reemplazo, cancelación segura, limpieza de temporales, contenido inválido y versión incompatible.
- `PamAutomation.Tests` ahora referencia `PamAutomation.Infrastructure` para probar la implementación concreta.
- No se conectó todavía la persistencia con la interfaz y no se implementaron Credential Manager, DPAPI, correo, TOTP, Edge o PAM.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 4: credenciales PAM protegidas

- El usuario confirmó que las fases 3A y 3B compilaron y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregaron en Core `PamCredentials`, `PamCredentialUpdate`, `IPamCredentialStore` y errores estables de almacenamiento.
- Se implementó `WindowsPamCredentialStore` sobre `CredReadW`, `CredWriteW`, `CredDeleteW` y `CredFree`, sin dependencias NuGet nuevas.
- Usuario, contraseña y semilla TOTP se guardan juntos en una única credencial genérica versionada bajo `PamAutomation/PamCredentials/v1`.
- Una edición con campos vacíos conserva internamente los valores protegidos existentes; una primera configuración incompleta se rechaza.
- El blob binario incluye versión y longitudes, respeta los límites de Windows Credential Manager y sus copias administradas y no administradas se limpian con ceros después de usarse.
- Los modelos sensibles redactan `ToString()` y su presentación en el depurador; los mensajes controlados no contienen valores de credenciales.
- Se agregaron pruebas MSTest con una API de Windows falsa para no crear, leer, modificar ni eliminar credenciales reales.
- Se eliminó de `README.md` una frase heredada sobre una carpeta común de datos que contradecía la regla canónica de no persistir archivos operativos.
- La interfaz todavía no usa este almacén y no se implementaron TOTP, Edge, acceso a PAM, DPAPI de correo u Outlook.
- La revisión estática quedó a cargo de esta fase; la compilación y ejecución de pruebas quedaron pendientes en ese momento.

## 9 de septiembre de 2026 — Fase 5: generación TOTP

- El usuario confirmó que la fase 4 compiló y que todas sus pruebas terminaron correctamente con `Release | x64`.
- Se agregó en Core un decodificador Base32 estricto que acepta el formato agrupado con espacios mostrado por Microsoft Authenticator, sin registrar ni reproducir la clave en errores.
- Se implementó RFC 6238 con HMAC-SHA1, intervalos de 30 segundos y códigos de seis dígitos.
- El resultado representa explícitamente el inicio y la expiración UTC del código y permite exigir al menos ocho segundos de vigencia antes de enviarlo.
- El código TOTP se oculta en `ToString()` y en el depurador; las copias binarias administradas de la clave y del hash se limpian después del cálculo.
- Se agregaron pruebas MSTest con vectores públicos RFC 6238, formatos Base32 válidos e inválidos, límites de ventana, vigencia mínima y redacción del resultado.
- La validación inicial detectó una ambigüedad de precedencia entre el operador módulo y una expresión `switch`; se hizo explícito el residuo Base32 para rechazar correctamente longitudes de un solo símbolo.
- Se completó `Fases.md` como resumen acumulativo de las etapas construidas y se añadió al inventario documental.
- No se utilizó la clave real compartida por el usuario, no se leyeron credenciales protegidas, no se abrió Edge, no se contactó PAM y no se envió correo.
- La hora HTTP del portal y su abstracción de reloj quedan para la fase 6.

## 9 de septiembre de 2026 — Flujo de desarrollo exclusivo por terminal

- El usuario sustituyó la decisión anterior y estableció que restauración, compilación, ejecución, pruebas y publicación deben realizarse desde la terminal.
- Se confirmó `dotnet` en `PATH` con el SDK x64 de .NET 10.0.400.
- La solución se restauró y compiló con `Release | x64`: 0 errores y una advertencia heredada en `TestExplorerSmokeTests.cs` por una aserción siempre verdadera.
- El intento con `dotnet test` confirmó que el destino VSTest no es compatible con Microsoft Testing Platform 2.1 bajo .NET 10 sin seleccionar el runner MTP.
- El runner integrado se ejecutó directamente mediante `dotnet run` sobre `PamAutomation.Tests`: 71 pruebas correctas, 0 fallidas y 0 omitidas.
- La documentación canónica se actualizó para usar exclusivamente los comandos `dotnet` desde la terminal y dejar de requerir un entorno gráfico de desarrollo.

## 9 de septiembre de 2026 — Protocolo de trabajo con subagentes

- Se documentó en `Subagentes.md` el uso recomendado de GPT-5.6 Sol `medium` como coordinador y hasta dos GPT-5.6 Terra `medium` como implementadores.
- Se estableció que el coordinador conserva la responsabilidad de dividir, revisar, integrar y validar todo el trabajo.
- Se aclaró que el aislamiento no debe asumirse: las escrituras paralelas requieren ramas y worktrees preparados explícitamente, cuya base es una instantánea y no se sincroniza automáticamente.
- Se definieron contratos de delegación, límites de archivos, integración secuencial, validación `Release | x64`, protección de secretos y condiciones obligatorias para detenerse.
- Se incluyeron medidas para limitar el consumo de tokens y reservar razonamiento `high` para revisiones puntuales complejas o de seguridad.
- Se distinguió la política de lectura por rol: el coordinador conserva la lectura completa y entrega a cada subagente un paquete de contexto con rutas, alcance, restricciones y secciones concretas.
- Los subagentes no deben recorrer todo el repositorio ni releer todos los Markdown por defecto; solo pueden ampliar la lectura ante una dependencia directa, un riesgo de seguridad o una contradicción, dejando constancia de ello.
- Se alinearon `AGENTS.md`, `CODEX_BUILD_SPEC.md` y `Subagentes.md` para eliminar la obligación contradictoria de repetir toda la documentación en cada agente delegado.
- Se refinó la lectura documental del coordinador: debe completarla únicamente la primera vez que un thread de Codex entra al proyecto sin contexto previo. Las nuevas tareas, fases o prompts dentro del mismo thread conservan el contexto y usan `AGENTS.md` como índice para consultar solo lo necesario; una edición hecha por el propio agente tampoco fuerza otra lectura completa.
- Se convirtió el máximo de dos subagentes en un límite absoluto por task, no en una cantidad que deba agotarse.
- Cada subagente de implementación debe recibir una unidad vertical independiente y ser responsable de su código, sus pruebas relacionadas y su validación focalizada; queda descartado dividir una misma característica entre un productor de código y otro agente que espere para escribir sus pruebas.
- Se adoptó un trabajo por oleadas: el coordinador fija primero contratos y propietarios de archivos compartidos, delega hasta dos tareas que puedan avanzar sin esperarse, e integra y valida un resultado a la vez antes de iniciar trabajo dependiente.
- Se hizo explícito que un cambio pequeño, concentrado o sin paralelismo real debe implementarlo directamente el agente principal para evitar el consumo adicional de contexto, supervisión, revisión e integración.
- `Fases.md` ahora clasifica cada fase pendiente como independiente, independiente condicionada o dependiente, registra sus prerrequisitos y propone únicamente dos combinaciones iniciales de paralelismo: fases 6 con 7 y, después de integrarlas, fase 8 con 14.
- La clasificación de paralelismo no autoriza automáticamente a crear subagentes: el coordinador debe volver a comprobar contratos, archivos y tamaño real antes de cada oleada.
- Este cambio fue exclusivamente documental; no se modificó código, no se ejecutaron integraciones reales y no se crearon subagentes.

## 9 de septiembre de 2026 — Fases 6 y 7: reloj HTTP y sesión Edge restringida

- Se desarrollaron en paralelo mediante un subagente aislado por fase y se integraron secuencialmente después de revisar y validar cada resultado.
- La fase 6 agregó contratos de reloj en Core y una implementación en Infrastructure calibrada con HTTP `Date`, resolución del encabezado, mitad del tiempo de ida y vuelta y avance monotónico, sin respaldo al reloj local.
- La fase 7 agregó Microsoft.Playwright 1.62.0, contratos de sesión en Core y una implementación en Browser que usa Edge externo visible con `Channel = "msedge"` y un contexto temporal.
- El contexto restringe solicitudes HTTP(S) al origen exacto `https://10.30.17.117`, bloquea service workers y limita `IgnoreHTTPSErrors` a la sesión PAM.
- El contrato de navegación expone solamente URI final, estado y HTTP `Date`; no enumera ni entrega cookies, tokens u otras cabeceras.
- Se añadieron errores estables para distinguir un fallo al lanzar Edge de un fallo al inicializar la sesión y se garantizó la liberación de recursos creados parcialmente.
- Las pruebas usan adaptadores falsos y no abren Edge, no contactan PAM, no leen credenciales reales, no crean solicitudes y no envían correo.
- La validación consolidada con `Release | x64` compiló sin errores y ejecutó 90 pruebas correctas; permanece una advertencia MSTest heredada en `TestExplorerSmokeTests.cs`.
