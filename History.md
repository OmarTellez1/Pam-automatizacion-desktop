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

## 9 de septiembre de 2026 — Fases 8 y 14: autenticación simulada y Outlook clásico

- Se desarrollaron en paralelo mediante dos subagentes nativos y worktrees separados, con integración y validación secuencial por el coordinador.
- La fase 8 agregó un servicio de autenticación simulado que coordina sesión Browser, reloj HTTP, TOTP, espera de ventana segura, reintento único y confirmación final mediante contratos testeables.
- `PamNavigationResponse` ahora incluye el tiempo monotónico de ida y vuelta utilizado para calibrar el reloj del portal.
- La fase 14 agregó contratos y modelos redactados de Outlook, validación de cuenta y mensaje, ejecución STA y un adaptador Microsoft Office Interop/COM con liberación explícita.
- Se actualizó `Microsoft.Office.Interop.Outlook` a 15.0.4797.1004, que restauró y compiló sin la advertencia `NU1701` observada con la versión anterior.
- Las pruebas de Outlook usan un seam COM falso; no abren Outlook ni envían mensajes. El resultado posterior a `Send()` queda como no confirmado hasta consultar Enviados.
- La herramienta de parche de los subagentes nativos quedó inicialmente anclada al checkout principal aunque sus comandos estaban en los worktrees. Ambos se detuvieron o fueron interrumpidos; los cambios se replicaron mediante rutas verificadas hacia cada worktree y el checkout principal se limpió antes de integrar.
- La validación consolidada compiló en `Release | x64` con 0 errores y ejecutó 116 pruebas correctas. No se abrió Edge, no se contactó PAM, no se inició Outlook real y no se envió correo.

## 9 de septiembre de 2026 — Fase 9: autenticación PAM real visible

- El usuario autorizó explícitamente una prueba real limitada al login contra `https://10.30.17.117/webconsole/login?redir=dashboard` y prohibió solicitar accesos.
- Las credenciales proporcionadas se guardaron en Windows Credential Manager bajo `PamAutomation/PamCredentials/v1`; no se incorporaron a archivos, código, logs, capturas ni documentación.
- Se implementaron `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`, enlazados exclusivamente con la página del contexto temporal creado por `PamBrowserSessionFactory`.
- Se centralizaron los nombres accesibles observados para usuario, contraseña, inicio de sesión, desafío TOTP, envío y confirmación del dashboard.
- La primera integración descubrió que el portal emite `Navigation complete.` mediante `role=alert`; se corrigió su clasificación para no confundir ese anuncio de transición con un rechazo de credenciales.
- Los estados ambiguos o agotados no se convierten en rechazos explícitos y, por tanto, no habilitan reintentos adicionales.
- Una integración posterior abrió Edge externo visible, utilizó el contexto no persistente y la hora HTTP del portal, completó el TOTP y confirmó `Return to dashboard`.
- La centralización inicial del patrón de rechazo TOTP añadió `RegexOptions.CultureInvariant`, una bandera que Playwright .NET no admite en locators; la integración se detuvo antes de introducir el código. Se retiró exclusivamente esa bandera y la validación real final volvió a confirmar el dashboard.
- La ejecución terminó inmediatamente después del login y cerró el contexto; no navegó a Favorites, no abrió formularios, no creó solicitudes, no inició Outlook y no envió correo.
- La validación consolidada compiló en `Release | x64` con 0 errores y ejecutó 127 pruebas correctas, 0 fallidas y 0 omitidas. Permanece la advertencia heredada `MSTEST0032` fuera del alcance de esta fase.

## 9 de septiembre de 2026 — Fase 10: navegación de solo lectura a Favorites

- El usuario autorizó continuar con la fase 10 utilizando el agente principal y sin subagentes; la inspección real se limitó a navegación y lectura, manteniendo prohibido solicitar accesos.
- `PamAuthenticationService` ahora implementa `IPamAuthenticatedSessionFactory` y puede transferir al consumidor una sesión autenticada confirmada; el consumidor conserva la responsabilidad de cerrarla. El método de verificación anterior sigue cerrando automáticamente su propia sesión.
- Se agregaron `PamFavoriteAccount`, errores estables, contratos de portal y `PamFavoritesService` en Core. Las identidades se validan como completas y únicas y su representación textual permanece redactada.
- `PamAutomation.Browser` navega a Accounts, identifica `Browse by Category > Favorites`, espera filas completas y estables, lee `SystemName`, `ApplicationName` y `AccountName` y recorre el paginador accesible cuando existe, con un límite seguro de 500 páginas.
- El contrato de fase 10 no permite seleccionar filas, abrir formularios ni enviar solicitudes. Los valores observados se conservaron únicamente en memoria y no se imprimieron ni persistieron.
- La integración `PAM_AUTOMATION_ALLOW_REAL_FAVORITES=READ_ONLY` abrió Edge visible, reutilizó la sesión autenticada, confirmó cuentas válidas en Favorites y cerró el contexto temporal.
- No se abrieron formularios, no se pulsó `Submit Request`, no se crearon solicitudes, no se inició Outlook y no se envió correo.
- La validación consolidada compiló en `Release | x64` y ejecutó 146 pruebas correctas, 0 fallidas y 0 omitidas. La advertencia heredada `MSTEST0032` permanece fuera del alcance de esta fase.

## 9 de septiembre de 2026 — Fase 11: modo de prueba de solicitudes

- El usuario autorizó el desarrollo con el agente principal y la prueba real visible de formularios, manteniendo prohibido crear solicitudes.
- Se agregó `PamRequestTestService`, que reutiliza una única sesión autenticada para leer Favorites y procesar cada formulario.
- `IPamRequestTestPortal` solo permite validar y descartar; deliberadamente no contiene una operación de envío.
- El adaptador Browser vuelve a comprobar la identidad de cada fila, abre `Access`, selecciona la pestaña del formulario, llena fecha, hora, duración y razón, verifica `Application Session` y ejecuta `Cancel > Discard Changes`.
- Antes de abrir formularios se instala un bloqueo de red para el endpoint de creación; cualquier intento de `POST /api/ps-portal/requests` se aborta y se informa como fallo.
- La primera integración se detuvo de forma segura porque React restableció `Start Time`; se incorporó el segundo llenado condicionado ya conocido en la referencia anterior.
- Otra ejecución reveló que ciertos formularios abren Flatpickr automáticamente; la selección de fecha se hizo idempotente para no cerrar el calendario con un clic redundante.
- La integración final abrió Edge visible y confirmó 14 `Favorites` rows encontradas, 14 formularios validados y descartados y `submitted = 0`.
- No se creó ninguna solicitud, no se abrió Outlook, no se envió correo y no se imprimieron ni persistieron identidades, credenciales o valores del formulario.
- La solución compiló en `Release | x64` con 0 errores y el proyecto de pruebas ejecutó 154 pruebas correctas. Permanece la advertencia heredada `MSTEST0032`, fuera del alcance de la fase.

## 9 de septiembre de 2026 — Plan post-migración: colecciones personalizadas

- El usuario definió una ampliación nueva que no existe en la aplicación JavaScript anterior y debe comenzar solo después de completar la migración.
- Una colección local permitirá seleccionar cualquier subconjunto de `Favorites` rows, asignarle un nombre editable y conservar fecha, hora, duración y razón predeterminadas.
- Se registró como fase 20, dependiente de la finalización de la migración y de las pruebas administradas de fase 19; no cambia el alcance de fase 12.
- Se tomó la referencia visual como guía para día, mes, descripción completa de fecha, entrada de hora con `AM/PM`, duración en horas, razón y guardado explícito.
- Se previeron sobrescrituras opcionales por `Favorites` row para solicitudes personalizadas, cuya experiencia exacta deberá confirmarse al diseñar la fase.
- `Favorites` permanecerá como fuente de verdad. Las colecciones guardarán `System + Application + Account` y se reconciliarán con todas las páginas para detectar filas `Available`, `Missing`, `New in Favorites` o `Ambiguous` sin sustituciones silenciosas.
- Las colecciones se tratarán como configuración privada, versionada, atómica y protegida con DPAPI `CurrentUser`; no contendrán credenciales, TOTP, cookies ni resultados.
- Se creó `PAM_NAVIGATION.md` con el recorrido web confirmado, selectores, comportamiento observado de los controles e invariantes necesarios para implementar colecciones sin depender de índices del grid.
- El usuario aclaró que los nombres visibles del portal deben conservarse en inglés. Se documentaron `Account`, `Description`, `System`, `Account Status`, `IP Address`, `Platform` y `Application`, y se reservó `Favorites` row para referirse inequívocamente a una fila completa.
- Se dejó explícito que `Account` no identifica por sí solo una fila: puede repetirse y la clave confirmada hasta fase 11 es `System + Application + Account`.
- Este cambio fue exclusivamente documental: no se inició la fase 12, no se modificó código, no se abrió PAM y no se creó ninguna solicitud.

## 10 de septiembre de 2026 — Fase 12: solicitudes reales confirmadas

- El usuario autorizó el desarrollo con el agente principal, sin subagentes, y permitió exactamente una solicitud PAM real para hoy con duración de 5 minutos o el mínimo disponible.
- Se agregó `IPamRequestSubmissionPortal` como contrato separado del modo de prueba. El contrato seguro de fase 11 conserva su bloqueo de red y continúa sin capacidad de envío.
- `PamRequestSubmissionService` valida la configuración, reutiliza una sola sesión autenticada, vuelve a leer todas las `Favorites` rows y exige una autorización ligada al conteo y a la configuración exactos mostrados al usuario.
- Si `Favorites` o la configuración cambian después de confirmar, el flujo termina antes de crear el portal de envío. Antes de cada fila también se respeta la cancelación; después del clic se espera la respuesta para no presentar como cancelada una mutación cuyo estado remoto sea ambiguo.
- Browser vuelve a localizar la fila por `System + Application + Account`, abre `Access > Submit Request`, llena el formulario y verifica inmediatamente sus valores y `Application Session` antes del botón final.
- El envío espera específicamente `POST /api/ps-portal/requests`. Solo 2xx se considera `Submitted`; el 400 con código exacto `PSPortal.Requests.Policies.RemoteAppAccesssAlreadyAvailable` se clasifica como conflicto conocido y permite continuar; cualquier otra respuesta detiene el proceso sin exponer el payload.
- Los resultados se copian y permanecen en memoria. Sus representaciones textuales están redactadas y no se añadieron logs persistentes ni datos de cuenta a la salida de pruebas.
- La integración destructiva quedó aislada bajo la categoría `ExplicitPamMutation`, el opt-in exacto `PAM_AUTOMATION_ALLOW_SINGLE_REAL_REQUEST=CREATE_ONE_REQUEST_TODAY` y un límite interno de un solo intento. No se ejecuta como parte del conjunto ordinario.
- La única ejecución autorizada creó exactamente una solicitud el 10 de septiembre de 2026, con inicio `08:20 AM` normalizado como `08:20` y duración `Days = 0`, `Hours = 0`, `Minutes = 5`. PAM devolvió éxito y mostró `Successfully submitted request.`; no se intentó otra fila.
- Se guardaron localmente `img/fase12-request-form-before-submit.png` y `img/fase12-request-result-after-submit.png`, ambas excluidas de Git. Las capturas documentaron la estructura y terminología del portal sin contener credenciales, TOTP, cookies o tokens.
- No se abrió Outlook ni se envió correo. La integración real no debe repetirse para validaciones posteriores; las comprobaciones restantes de esta fase son locales y no destructivas.
- La validación final no destructiva compiló la solución en `Release | x64` con 0 errores y ejecutó 174 pruebas correctas, 0 fallidas y 0 omitidas. Solo permanece la advertencia heredada `MSTEST0032`, fuera del alcance de esta fase.

## 10 de septiembre de 2026 — Fase 13: lectura y comparación de Pending

- El usuario autorizó continuar con el agente principal, sin subagentes, y hasta dos pruebas reales si eran necesarias.
- Se agregaron contratos separados de solo lectura, modelos redactados, errores estables y `PamPendingService` para leer y comparar solicitudes `Pending` sin persistir Request ID.
- Browser navega a `Requests`, selecciona exactamente `Status = Pending`, admite el estado vacío, recorre hasta 500 páginas, rechaza Request ID inválidos o duplicados y reintenta un snapshot cambiante hasta tres veces.
- La comparación exige `Request Start Date`, `System`, `Account`, `Application`, `Expires` y `Description`. Coincidencias múltiples se rechazan como ambiguas y faltantes se conservan explícitamente.
- Se añadió una espera acotada de hasta seis lecturas separadas por dos segundos para la consistencia eventual posterior a una creación.
- La primera prueba real abrió Edge visible y confirmó `Requests`, `Status = Pending`, `0 items`, `There are no records to display.` y el paginador vacío. No creó ni modificó solicitudes.
- Una prueba inicial limitada recibió `ApprovedAccessOverlap` y no creó solicitudes. Después el usuario amplió la autorización para solicitar accesos con hora actual del sistema y duración de 5 minutos.
- Una prueba creó una solicitud de 5 minutos con inicio ajustado al siguiente minuto. La lectura inmediata confirmó una fila, un Request ID numérico, `Response = Pending`, el formato inglés de `Request Start Date` y todos los identificadores DOM de columna; la reconciliación estricta detectó una diferencia aún no modelada en `Application`.
- Dos bloques posteriores de tres intentos terminaron únicamente en `ApprovedAccessOverlap`; crearon cero solicitudes adicionales. No se siguió ampliando el alcance.
- Siguiendo la corrección del usuario se probaron el 10 de septiembre de 2026 solicitudes de `05:05 PM` a `05:10 PM`. La primera ejecución creó una fila; un intento de recuperación defectuoso no la reconoció y creó una segunda fila idéntica autorizada. Se detuvieron inmediatamente las mutaciones y no se creó una tercera solicitud.
- Una lectura posterior exclusivamente no destructiva confirmó que `System` y `Account` coincidían de forma única y que Pending representa `Application` como `nombre solicitado - destino`. La regla final valida el nombre completo solicitado y permite únicamente ese sufijo estructurado; no usa familias ni deriva valores desde `System`.
- La prueba final fue idempotente: reconoció individualmente una solicitud autorizada existente, validó `Request Start Date = 05:05 PM`, `Expires = 05:10 PM`, `Description`, `Application`, `System`, `Account` y `Response = Pending`, e informó cero solicitudes nuevas.
- Se conservaron tres capturas locales ignoradas por Git: `img/fase13-pending-grid.png`, `img/fase13-pending-grid-description-and-expiration.png` e `img/fase13-pending-grid-application.png`. Pueden incluir datos operativos visibles de las filas, pero nunca credenciales, TOTP, cookies o tokens; no deben versionarse ni copiarse a documentación.
- La validación final no destructiva compiló en `Release | x64` con 0 errores y ejecutó 208 pruebas correctas. Permanece únicamente la advertencia heredada `MSTEST0032`.

## 10 de septiembre de 2026 — Fase 15: composición HTML saneada

- El usuario autorizó desarrollar la fase con el agente principal y sin subagentes. Aunque existía autorización condicionada para pruebas reales, no fueron necesarias: no se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.
- Se agregó `OutlookHtmlComposer` en Core con parser XML de .NET, DTD prohibidos, límite de 8 millones de caracteres y una allowlist pequeña para texto enriquecido, listas, tablas y referencias de imagen `cid:`.
- El saneamiento elimina elementos activos, atributos de eventos, enlaces, recursos externos, atributos desconocidos y declaraciones CSS no permitidas. Los errores usan códigos estables y mensajes que no reproducen el HTML privado.
- `{{REQUEST_IDS}}` debe existir exactamente una vez como texto. Los Request ID deben ser numéricos y únicos; se insertan en el orden recibido y uno por línea mediante `<br />`.
- `OutlookMessageValidator` valida otra vez el cuerpo final antes de crear COM. Rechaza HTML alterado, contenido que aún tenga el marcador y cuerpos que no contengan todos los Request ID como tokens numéricos completos.
- Se documentó que el adaptador WPF desde `FlowDocument`, la persistencia DPAPI del editor y la materialización de adjuntos CID siguen pendientes; esta fase establece el límite seguro que deberán consumir.
- La solución compiló en `Release | x64` con 0 errores y la suite ordinaria ejecutó 225 pruebas correctas. Solo permanece la advertencia heredada `MSTEST0032`, fuera del alcance de esta fase.

## 10 de septiembre de 2026 — Fase 16: resultados en memoria e interfaz

- El usuario autorizó desarrollar la fase con el agente principal y hasta diez pruebas reales. No fue necesario abrir PAM ni crear solicitudes; la cuota consumida fue cero.
- Se introdujo `IPamAutomationExecutionService` y su implementación en Core para preparar el conteo dinámico de `Favorites`, ejecutar el modo real detrás de una confirmación de un uso, reconciliar `Pending` y devolver resultados defensivos exclusivamente en memoria.
- `PamExecutionResult` conserva modo, hora, acción final, conteos, conflictos y Request ID mediante copias defensivas; su representación textual no enumera identificadores.
- La aplicación ahora compone las implementaciones reales en `App.xaml.cs`, carga `app-settings.json` y Windows Credential Manager, y conecta comandos asíncronos de prueba y ejecución real.
- Se añadieron ventanas WPF nativas para guardar credenciales sin recuperar secretos y configurar fecha, hora de 12 horas con `AM/PM`, duración, razón y `testModeEnabled`.
- Antes de crear solicitudes, un diálogo modal muestra el conteo y la configuración exacta. Rechazarlo termina sin invocar el envío. Durante todo el flujo los controles permanecen deshabilitados.
- El panel y diálogo de resultado muestran procesadas, Request ID, conflictos y hora final. Los errores controlados mantienen sus mensajes seguros y los desconocidos se sustituyen por un texto fijo.
- Se completó `mail-settings.dat` con escritura atómica y cifrado DPAPI `CurrentUser`. Contiene remitente, destinatarios, asunto, XHTML saneado e imágenes PNG inline; las representaciones textuales permanecen redactadas.
- El editor WPF usa `RichTextBox`/`FlowDocument` y admite negrita, cursiva, subrayado, listas, tablas e imágenes pegadas. Su convertidor realiza ida y vuelta con XHTML permitido y genera referencias `cid:`; Outlook materializa cada PNG en un temporal único, lo agrega y elimina el temporal en `finally`.
- Guardar correo valida la cuenta mediante un mensaje descartado. El envío de prueba de Outlook y el correo del modo de prueba PAM son acciones distintas, con destinatarios y confirmaciones independientes según la especificación.
- Se implementó protección contra duplicados en memoria y mediante una propiedad privada de Outlook con el asunto y conjunto exacto de Request ID. Se consultan Enviados, Bandeja de salida y Borradores antes y después de enviar; la Bandeja de salida se mueve a Borradores y los estados ambiguos no se marcan como confirmados.
- Los fallos parciales de envío PAM conservan el conteo procesado y la interfaz lo presenta sin identidades ni detalles sensibles, indicando revisar `Pending` antes de reintentar.
- La compilación final en `Release | x64` tuvo 0 errores y la suite ordinaria ejecutó 243 pruebas correctas. No se abrió PAM ni Outlook, no se creó ninguna solicitud y no se envió correo; se consumieron cero de las diez pruebas reales autorizadas. Computer Use no expuso superficies nativas en esta sesión, por lo que la revisión visual final queda para el usuario en Visual Studio.

## 10 de septiembre de 2026 — Fase 16.5: paridad funcional y claridad de configuración

- Se inspeccionó en Edge visible la aplicación JavaScript anterior disponible en `http://127.0.0.1:4317/` y se contrastaron inicio, credenciales, solicitud y correo con las capturas locales aportadas por el usuario. No se guardaron datos ni se activaron acciones externas durante la inspección.
- La aplicación anterior quedó como referencia de claridad y paridad funcional, no como dependencia ni objetivo de copia exacta. La portada WPF existente se conservó y el trabajo se concentró en las tres ventanas de configuración.
- `Configurar credenciales` pasó a presentar tarjetas separadas para `Credenciales del PAM` y `Cuenta de Outlook`, estados configurado/no configurado, ayuda contextual, revelado temporal de los secretos recién introducidos y resultados dentro de la propia ventana.
- `Guardar cambios y probar acceso` actualiza Windows Credential Manager y realiza un login PAM visible no destructivo en una sesión temporal. `Guardar cambios y verificar Outlook` comprueba la cuenta exacta antes de persistirla; el correo de prueba continúa separado, exige confirmación y solo utiliza la misma dirección como remitente y destinatario.
- Se creó `outlook-account.dat` para desacoplar la cuenta de Outlook de la plantilla. El documento tiene esquema, cifrado DPAPI `CurrentUser` y escritura atómica; al iniciar, una instalación con remitente antiguo en `mail-settings.dat` lo migra automáticamente.
- La solicitud ahora muestra fecha completa en español, campo `hh:mm` con `AM/PM`, bloques verticales para duración y razón, modo de prueba secundario y errores dentro del formulario.
- El editor de correo conserva formato, listas, tablas e imágenes, muestra la cuenta remitente como solo lectura, explica Outlook clásico y la separación de destinatarios, e incorpora `Insertar {{REQUEST_IDS}}` con conteo y validación visibles.
- La validación final se realizó con el SDK 10.0.401 y runtime x64 10.0.12. El muxer x64 canónico `C:\Program Files\dotnet\dotnet.exe` no estaba presente, por lo que se ejecutó `MSBuild.dll` con el host x64 instalado por Visual Studio y el ejecutable x64 del runner MSTest, sin modificar el sistema.
- La solución compiló sin errores y la suite ordinaria ejecutó 253 pruebas correctas, incluida una prueba STA que carga conjuntamente las tres ventanas de configuración. No se abrió PAM ni Outlook, no se enviaron correos y no se crearon solicitudes.

## 11 de septiembre de 2026 — Fase 16.6: logging seguro de desarrollo

- El usuario solicitó logging local en la raíz del repositorio, con retención de dos días y cobertura suficiente para depurar operaciones importantes, sin subagentes.
- Se implementó un proveedor basado en `Microsoft.Extensions.Logging` que solo se activa con depurador conectado o `PAM_AUTOMATION_DEVELOPMENT_LOGGING=1` y exige encontrar `.git` y `PamAutomation.slnx` antes de crear `logs`.
- Los eventos cubren ciclo de vida WPF, inicialización, configuración, ejecución y etapas COM de Outlook. Las excepciones conservan tipo, `HRESULT` y stack trace, pero no su mensaje; tampoco se registran direcciones, credenciales, TOTP, cookies, tokens, Request ID ni contenido del correo.
- Los archivos diarios `pam-automation-AAAA-MM-DD.log` se eliminan automáticamente cuando superan dos días. La publicación normal conserva un logger nulo y no genera archivos operativos.

## 11 de septiembre de 2026 — Fase 16.7: corrección de activación COM de Outlook

- Una prueba manual del usuario reprodujo el rechazo de configuración y el nuevo log ubicó el fallo en `ComApplicationCreate`: `FileNotFoundException`, `HRESULT 0x80070002`.
- Se confirmó que `Microsoft.Office.Interop.Outlook.dll` exigía `office, Version=15.0.0.0`, ausente de la salida y de `PamAutomation.App.deps.json`; no era un problema del SMTP, la cuenta, la sesión de Outlook ni DPAPI.
- Se retiró la PIA administrada y `OutlookComApplicationFactory` pasó a activar `Outlook.Application` mediante `ProgID` y despacho COM tardío nativo de C#. Se mantuvieron STA, interfaces internas, resolución exacta, carpetas, huellas, adjuntos CID y liberación explícita.
- Las salidas Debug y Release ya no contienen ni declaran `Microsoft.Office.Interop.Outlook`, `office.dll` o `stdole.dll`. La solución compiló y las 257 pruebas ordinarias finalizaron correctamente; no se abrió Outlook, no se envió correo, no se contactó PAM y no se crearon solicitudes.

## 11 de septiembre de 2026 — Fase 16.8: retroalimentación visual de credenciales y Outlook

- El usuario confirmó manualmente que la cuenta Outlook ya se verifica y que el correo de prueba llega correctamente, pero señaló que `SubmittedUnconfirmed` generaba dudas y que faltaba una señal visible durante las esperas.
- Las comprobaciones de PAM y Outlook ahora muestran `Solicitud recibida`, un indicador circular animado y controles deshabilitados mientras trabajan. El indicador se oculta al mostrar el resultado o una cancelación.
- El correo de prueba muestra `El correo de prueba se envió con éxito` cuando COM acepta `Send()`. `NotConfirmed` y cualquier estado no exitoso se presentan como fallo, no como panel verde.
- La primera prueba manual al abrir `Editar credenciales` reveló una excepción `Storyboard.ResolveTargetName`: el template intentaba iniciar al cargarse una animación dirigida al nombre `SpinnerRotation`, incluso con el indicador oculto.
- Se eliminó el disparador automático del template. Cada comprobación crea e inicia directamente su `RotateTransform` y lo detiene al presentar éxito, error o cancelación; abrir la ventana no mantiene animaciones activas.
- El límite asíncrono de la ventana captura fallos inesperados, registra la etapa, presenta un mensaje seguro y restaura los controles en el `Dispatcher`. Los fallos al iniciar o detener el indicador también quedan contenidos y registrados.
- El manejador global del `Dispatcher` conserva el diagnóstico y, ante una excepción WPF que escape de sus límites locales, muestra un aviso seguro y cierra ordenadamente la aplicación; las tareas no observadas se marcan como observadas después de registrarse.
- La solución compiló en `Release | x64` y las 260 pruebas ordinarias finalizaron correctamente. Las nuevas pruebas renderizan la ventana, verifican el ciclo completo del indicador y fuerzan un fallo inesperado sin abrir PAM ni Outlook, crear solicitudes o enviar correos.

## 11 de septiembre de 2026 — Fase 17: diagnóstico seguro exportable

- Se incorporó `RuntimeDiagnosticLoggerFactory`, que conserva únicamente los últimos 500 eventos seguros en memoria y continúa enviándolos al archivo de desarrollo de dos días cuando ese destino está explícitamente habilitado.
- El buffer guarda plantillas de mensaje, propiedades de estado o conteo permitidas y metadatos de excepción sin mensaje ni rutas de código. No recibe modelos de credenciales, configuración de correo ni resultados con valores de Request ID.
- `DiagnosticExportService` crea bajo acción explícita un ZIP con manifiesto, entorno, resumen reducido y log. Usa un temporal junto al destino, movimiento atómico y limpieza en `finally`; cancelar el selector no escribe nada.
- La portada añadió `Exportar diagnóstico`. Los fallos recuperables recomiendan usarlo y la barrera WPF fatal ofrece exportar el contexto antes de cerrar ordenadamente, sin crear archivos si el usuario responde que no.
- La compilación `Release | x64` finalizó con 0 errores y las 271 pruebas ordinarias fueron correctas. Las pruebas cubrieron seguridad del contenido, capacidad, cancelación, fallo, limpieza, restauración del comando y renderizado WPF sin contactar PAM u Outlook, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18: publicación portable y actualización informativa

- El usuario sustituyó el instalador MSI/MSIX por distribución manual de un ejecutable portable. Cada usuario elegirá su ubicación, descargará una nueva versión cuando corresponda y eliminará manualmente el ejecutable anterior.
- `PamAutomation.App` quedó versionado como `0.0.1`; Windows muestra producto, descripción y versión, y la interfaz usa el título `PAM Automation V. 0.0.1`.
- Se añadió `Portable-x64.pubxml`: `Release`, `win-x64`, self-contained, single-file, trimming/AOT desactivados y símbolos excluidos. La salida final contiene solo `PAM Automation.exe`, pesa 247.748.746 bytes y tiene SHA-256 `D6F001DA9D7F464BE21AE8B02BBF4EB3757A61662D738FADA3F248939BD3BE61`.
- El ejecutable se abrió desde `artifacts/publish/final/PAM-Automation-0.0.1`, mostró la versión correcta, permaneció respondiendo y extrajo en `%TEMP%` el driver Windows de Playwright incluido en el bundle. No generó archivos junto al ejecutable.
- Core incorporó el contrato de consulta; Infrastructure valida HTTPS, tamaño máximo, JSON, versión y repositorio; WPF consulta de forma asíncrona y muestra un banner descartable únicamente si la versión es superior. No hay descarga, reemplazo ni borrado automáticos.
- El `latest.json` público se visualizó sin autenticación y contiene `0.0.1`, pero su descarga redirige a `drive.usercontent.google.com`. Fortinet bloquea expresamente ese host y .NET recibe `PartialChain`; también falla el botón Descargar del navegador. Se conservó TLS estricto y la fase queda pendiente únicamente de sustituir el origen por un endpoint HTTPS permitido.
- La solución compiló en `Release | x64` y la suite ordinaria quedó ampliada a 280 pruebas. La integración real fallida fue de solo lectura; no se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.

## 11 de septiembre de 2026 — Fase 18: migración del manifiesto a GitHub

- El usuario creó el repositorio público `OmarTellez1/Pam-automation-installer`. La aplicación dejó de depender de Google Drive y pasó a consultar `https://raw.githubusercontent.com/OmarTellez1/Pam-automation-installer/main/latest.json`.
- `GoogleDriveApplicationUpdateService` se generalizó como `PublicManifestApplicationUpdateService`; conserva HTTPS obligatorio, timeout, cancelación, límite de 16 KiB, JSON estricto, versión numérica y coincidencia exacta del repositorio.
- La descarga pública del JSON funciona desde la red corporativa y con TLS válido. La integración todavía rechaza el contenido porque `repositoryUrl` conserva la dirección de Drive; el rechazo confirma la defensa prevista y la única acción externa pendiente es cambiar ese campo por `https://github.com/OmarTellez1/Pam-automation-installer`.
- Se documentó el flujo de release: incrementar metadatos, restaurar, compilar, probar, publicar con `Portable-x64`, adjuntar el ejecutable a un GitHub Release y actualizar `latest.json` al final.
- El usuario corrigió el manifiesto público. La integración explícita de solo lectura pasó 1/1 en 825 ms y confirmó HTTPS, TLS, JSON, versión y repositorio sin contactar PAM ni Outlook.
- Se regeneró la salida canónica `artifacts/publish/final/PAM-Automation-0.0.1/PAM Automation.exe`: es el único archivo de la carpeta, pesa 247.748.746 bytes y tiene SHA-256 `DBFD6226B3EC31CB6290C65C67B0B35D5ECE1F5BAFAF1D2168AC9484EE835D71`. Con esta evidencia la fase 18 quedó completada.

## 11 de septiembre de 2026 — Fase 18.5: interfaz compacta y hora separada

- La ventana principal pasó a iniciar maximizada y se redujeron botones, tarjetas, tipografías, márgenes y anchos del contenido para aprovechar mejor pantallas de trabajo.
- `CredentialsWindow`, `RequestSettingsWindow` y `MailSettingsWindow` recibieron dimensiones iniciales y mínimas más pequeñas. La barra de formato del correo usa un panel adaptable para envolver acciones cuando no caben horizontalmente.
- `Hora de inicio` dejó de ser un único campo `hh:mm`: `hh` es un desplegable de `01` a `12`, `mm` acepta hasta dos dígitos y se valida entre `0` y `59`, y `AM/PM` permanece separado. `RequestStartTimeInput` convierte la selección al formato de 24 horas.
- La compilación `Release | x64`, 14 pruebas focalizadas y las 290 pruebas ordinarias pasaron. No se abrió PAM ni Outlook, no se crearon solicitudes y no se enviaron correos.

## 11 de septiembre de 2026 — Fase 18.6: firma enriquecida y ventana de correo compacta

- La ventana de correo pasó a `700×620`, redujo su mínimo a `560×500` y compactó márgenes, avisos, editor y botones sin retirar scroll ni la barra adaptable.
- La aplicación anterior demostró que el caso de firma dependía de interceptar HTML y sus imágenes. La versión WPF ahora prioriza RTF de Outlook y, si solo existe CF_HTML, usa `RichClipboardHtmlImporter` con límites de tamaño, nodos y profundidad.
- Se añadió `HtmlAgilityPack` 1.13.0 como parser tolerante de 1,37 MB; el saneamiento propio sigue siendo la única autoridad. Se conservan estilos visuales seguros, enlaces `mailto:`/HTTPS, tablas e imágenes PNG/CID; se eliminan scripts, protocolos peligrosos y recursos remotos.
- `FlowDocumentHtmlConverter` conserva estilos, enlaces y dimensiones de imágenes durante la ida y vuelta, por lo que la firma no se pierde al guardar y reabrir la configuración DPAPI.
- La compilación `Release | x64`, 25 pruebas focalizadas y las 294 pruebas ordinarias pasaron. No se abrió Outlook, no se usó el portapapeles real, no se enviaron correos y no se contactó PAM.

## 11 de septiembre de 2026 — Fase 18.7: tablas pegadas y editor simplificado

- La aplicación JavaScript se revisó como referencia de lectura: su editor no ofrece una barra de construcción y al pegar prioriza `text/html`. La versión WPF adoptó esa misma interacción sin copiar su implementación ni relajar el saneamiento.
- Se retiraron las acciones de negrita, cursiva, subrayado, lista, creación de tabla e imagen. Solo permanece la inserción explícita de `{{REQUEST_IDS}}`; imágenes, tablas y formato se incorporan al pegar el contenido compuesto externamente.
- La conversión de tablas ahora conserva anchos de `colgroup`, `rowspan`, `colspan`, padding y bordes básicos. También crea un párrafo posterior cuando una tabla es el último bloque, permitiendo continuar escribiendo fuera de ella.
- La compilación `Release | x64` terminó con 0 errores y la suite ordinaria pasó 295/295. No se abrió Outlook, no se utilizó el portapapeles real, no se enviaron correos y no se contactó PAM.
- La primera corrección probó conversión y persistencia, pero asumía que WPF siempre entregaría `DataFormats.Html` como `string`; esa suposición no equivale a `clipboardData.getData("text/html")` del navegador y no cubría `text/html` ni streams. Se añadió normalización explícita de formatos y representaciones, logging seguro del recorrido y una prueba que carga el paquete resultante en el cursor real de un `RichTextBox`. La suite quedó en 298/298.

## 11 de septiembre de 2026 — Fase 18.8: tabla manual estable

- Se decidió conservar el pegado enriquecido para texto y firma, pero dejar de depender de tablas externas porque WPF no reproduce consistentemente todos los formatos que generan Outlook, navegadores y otras aplicaciones.
- `Editar envío de correo` muestra una advertencia y recupera una única acción `Insertar tabla 2×2`. La tabla controlada contiene las filas de solicitudes y motivo, columnas 30/70 y el marcador único.
- `FlowDocumentTableInserter` divide el párrafo en el cursor, inserta la tabla entre ambas partes, mantiene una línea posterior y evita anidarla si la acción se vuelve a pulsar con el foco dentro de ella.
- Una primera prueba reveló que modificar un `Run` mientras se enumeraba su `InlineCollection` invalida el enumerador; se corrigió trabajando sobre snapshots antes de modificar el marcador. La compilación quedó limpia y la suite pasó 301/301.

## 11 de septiembre de 2026 — Fase 18.9: reconciliación de Application observada

- El diagnóstico exportado de la primera ejecución completa confirmó 13 envíos correctos y un fallo posterior dentro de `PamPendingService.Compare`; el correo no llegó a ejecutarse.
- Las capturas aportadas demostraron coincidencia exacta de inicio `05:30 PM`, expiración `06:30 PM`, `Description = Pruebas`, `System` y `Account`. La diferencia estaba en la representación de `Application` entre Favorites y Pending.
- La comparación incorporó familias controladas para `NOSQLBOOSTER`, `PLSQL`, `PuTTY` y `WinSCP`, sin derivarlas desde `System` ni utilizar `Ticket`. Las aplicaciones desconocidas siguen requiriendo la regla estricta anterior y las colisiones de familia se rechazan como ambiguas.
- Se reprodujeron los nombres observados, incluido un mismo `System + Account` con PuTTY y WinSCP. La compilación `Release | x64` pasó y la suite terminó 307/307 sin contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18.10: aviso de inicio bloqueado

- Se confirmó que una solicitud con fecha u hora pasada deshabilitaba correctamente `Iniciar automatización`, pero el texto operativo podía seguir mostrando un estado ajeno, como `Configuración de credenciales sin cambios`.
- La ventana principal incorporó un aviso ámbar separado que explica la primera causa pendiente y señala la sección que debe corregirse. Los casos de fecha y hora vencidas tienen textos específicos.
- El aviso se actualiza al cargar o cambiar el estado de configuración y se oculta durante operaciones o cuando el inicio vuelve a estar habilitado.
- La compilación `Release | x64` pasó y la suite terminó 309/309 sin contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 18.11: resultado y correo sin estados ambiguos

- El resultado ya no abre un `MessageBox` dentro de `PresentResult`, antes de que el bloque `finally` terminara de restaurar `IsRunning`. Ahora se conserva en memoria y se consulta con el botón `Ver resultado de la automatización` cuando la interfaz ya está inactiva.
- La tarjeta persistente con resumen e IDs se sustituyó por ese único botón para evitar duplicar el contenido de la ventana de detalle.
- `SubmittedUnconfirmed` conserva su clasificación interna, pero muestra `El correo se envió correctamente desde Outlook.` cuando COM aceptó `Send()` sin error. Los estados de borrador, duplicado o fallo mantienen sus mensajes específicos.
- El cuerpo final aplica Aptos 11 con respaldos compatibles y respeta estilos explícitos internos. Las tablas pasan de 100% a 85%, centradas y con contenido alineado arriba.
- La compilación `Release | x64` pasó y la suite terminó 309/309 sin abrir Outlook, contactar PAM, crear solicitudes ni enviar correo.

## 11 de septiembre de 2026 — Fase 21: acceso interactivo persistente

- Se añadió `Acceder a PAM` como acción independiente de la automatización de solicitudes. Solo necesita credenciales PAM configuradas y reutiliza el login real con reloj HTTP y TOTP.
- Tras aclarar que el navegador debía ser normal y sobrevivir al cierre de la aplicación, se descartó la primera implementación parcial basada en una sesión temporal propiedad del ViewModel.
- La excepción nueva de `AGENTS.md` se aisló en `PamInteractiveBrowserSessionFactory`: perfil persistente dedicado, Edge iniciado como proceso externo y conexión CDP solamente en loopback durante la autenticación.
- `PamInteractiveAccessService` separa Playwright después de confirmar el dashboard sin cerrar el navegador. El flujo productivo de solicitudes continúa usando `PamBrowserSessionFactory`, contexto temporal, bloqueo de orígenes externos y disposición completa.
- Una sesión válida ya existente evita reenviar credenciales o TOTP. Los fallos de autenticación se presentan con los mensajes seguros existentes y nunca registran secretos.
- La compilación `Release | x64` terminó con 0 errores y 0 advertencias; la suite pasó 315/315. No se realizó un login PAM real ni se ejecutaron solicitudes o correos.

## 12 de septiembre de 2026 — Corrección de fase 21: certificado del perfil interactivo

- El diagnóstico exportado después de dos intentos confirmó que el navegador y la conexión CDP estaban disponibles, pero el primer `GotoAsync` fallaba antes de entregar usuario, contraseña o TOTP.
- Se contrastó en modo de solo lectura `C:\pam automatizacion\ABRIR_SESION_PAM.cmd` y su recorrido `scripts\open-manual-session.ps1`. La aplicación anterior usa el perfil habitual de Chrome, donde la confianza del certificado interno ya estaba resuelta; la nueva fase 21 partía de un perfil Edge dedicado limpio.
- El flujo interactivo ahora crea una sesión CDP de seguridad para la página, ignora el certificado exclusivamente durante la autenticación PAM, restaura la validación normal y separa Playwright sin cerrar Edge. Ante un fallo también intenta restaurar la validación antes de liberar recursos.
- Se añadieron logs de las etapas de conexión y excepción temporal sin registrar puerto, ruta, URL, credenciales ni TOTP. Una regresión verifica que la preparación segura ocurre antes de liberar o cerrar recursos.
- La compilación `Release | x64` pasó con la advertencia heredada `MSTEST0032`; la suite terminó 316/316. No se contactó PAM durante esta corrección.

## 12 de septiembre de 2026 — Fase 21.1: WebSocket CDP y preflight de Pending

- Un segundo diagnóstico mostró tres fallos `TimeoutException` dentro de `ConnectOverCDPAsync`: Edge sí había iniciado, pero la conexión no superaba el endpoint HTTP de descubrimiento.
- `PamInteractiveBrowserSessionFactory` ahora lee y valida puerto y ruta desde `DevToolsActivePort` y conecta al WebSocket exacto sobre `127.0.0.1`. Los timeouts transitorios participan en los reintentos controlados.
- La integración real autorizada inició sesión, confirmó el dashboard y terminó dejando abierto el Edge dedicado. No creó solicitudes ni envió correo.
- La ejecución real incorpora un preflight de `Pending`. Si existen filas exactamente iguales, muestra una decisión explícita para repetir o cancelar antes del primer envío.
- Los Request ID preexistentes se conservan solo en memoria y se excluyen de la reconciliación posterior. Esto permite identificar las nuevas filas cuando PAM admite varias solicitudes Pending iguales, sin confundirlas con las anteriores.
- La compilación `Release | x64` pasó y la suite ordinaria terminó 323/323; solo permanece `MSTEST0032`, heredada y ajena al cambio.

## 12 de septiembre de 2026 — Fase 21.2: Edge normal y confianza manual

- La comprobación del usuario aclaró que el Edge dedicado no debía considerarse un navegador normal: una descarga podía quedar asociada al entorno controlado y la conexión CDP seguía siendo un punto de fallo.
- Se volvió a contrastar el recorrido de solo lectura de `C:\pam automatizacion\scripts\open-manual-session.ps1` y se trasladó su técnica de accesibilidad a C#; no se modificó la aplicación anterior ni se incorporó PowerShell a producción.
- `Acceder a PAM` ahora abre el perfil habitual de Microsoft Edge sin `userDataDir`, CDP ni depuración remota. Windows UI Automation limita el control a ventanas `msedge`, al origen exacto de PAM y a los controles accesibles del login.
- Si Edge presenta el certificado interno, la aplicación muestra instrucciones y espera que el usuario elija manualmente `Avanzado` y `Continuar`; la automatización se reanuda al aparecer `Username`. La petición auxiliar para el reloj permite el certificado exclusivamente en el origen autorizado y no altera la confianza del navegador.
- El foco de ventana es una mejora opcional: una denegación de foco de Windows ya no aborta el flujo. Timeouts, cierre de ventana y controles ausentes producen errores controlados sin registrar secretos.
- La compilación `Release | x64` pasó; la suite ordinaria terminó 321/321 y una integración real autorizada confirmó el dashboard dejando Edge abierto. No se crearon solicitudes ni se envió correo; permanece la advertencia heredada `MSTEST0032`.

## 12 de septiembre de 2026 — Fase 21.3: zoom de PAM al 67 %

- Se trasladó a C# el comportamiento validado de la aplicación anterior: tras autenticar el dashboard se ejecuta `Ctrl+0` y cuatro veces `Ctrl+-` exclusivamente sobre la ventana PAM recién abierta.
- Antes de enviar teclas se vuelve a validar el origen, se enfoca la barra de direcciones y se comprueba el elemento que recibió el foco. Si Windows no permite demostrarlo, el zoom se omite de forma controlada y la sesión permanece abierta.
- La primera integración demostró que Edge no expone necesariamente el indicador como `Button`; la implementación final reconoce `67 %` en `Text`, `Group` o `Button` sin confundir porcentajes del contenido web.
- La segunda integración real confirmó explícitamente `67 %`, conservó Edge abierto y no creó solicitudes ni envió correo.
- El acceso directo se reubicó en el encabezado, entre `Exportar diagnóstico` y `Enviar correo con Outlook`, sin cambiar su comando ni reglas de habilitación.

## 12 de septiembre de 2026 — Fase 21.4: solicitudes en segundo plano

- El usuario decidió que la navegación de la automatización de solicitudes no debe mostrarse. `PamBrowserSessionFactory` ahora lanza el canal externo `msedge` con `Headless = true`.
- El cambio cubre el contexto temporal compartido por autenticación, `Favorites`, formularios y `Pending`, sin alterar su perfil aislado, política de origen, excepción HTTPS o disposición de recursos.
- `Acceder a PAM` permanece deliberadamente visible porque utiliza la fábrica interactiva separada y el Edge normal del usuario.
- La compilación `Release | x64` y las 12 pruebas de la fábrica pasaron. Un login real autorizado confirmó el dashboard en segundo plano y cerró el contexto sin visitar `Favorites`, crear solicitudes ni abrir Outlook.
