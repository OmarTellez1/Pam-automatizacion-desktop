# Estructura y arquitectura — Pam Automation .NET

## Propósito

Este documento describe:

1. La estructura física que existe actualmente en el repositorio.
2. La responsabilidad asignada a cada proyecto y módulo.
3. Las dependencias permitidas entre proyectos.
4. La arquitectura objetivo al finalizar la aplicación.

Debe actualizarse cada vez que se agregue, elimine o cambie de responsabilidad un proyecto, carpeta o módulo relevante.

La raíz oficial es:

```text
C:\pam automatizacion dotnet\PamAutomation
```

La aplicación anterior ubicada en `C:\pam automatizacion` no forma parte de esta estructura. Es únicamente una referencia funcional, histórica y visual de solo lectura.

## Estado actual

La solución utiliza `PamAutomation.slnx`, .NET 10 y la plataforma `x64`. Actualmente existen los límites principales de la arquitectura, configuración y credenciales protegidas, TOTP, reloj HTTP, sesión temporal `headless` de Edge, acceso PAM interactivo visible, autenticación PAM simulada y real, lectura de Favorites, validación y descarte seguro de formularios, solicitudes reales confirmadas, lectura y reconciliación de Pending, composición HTML saneada, el adaptador testeable de Outlook clásico y la coordinación del flujo PAM con resultados WPF mantenidos en memoria. La fase 16.5 añadió paridad funcional y estados claros a las ventanas de configuración sin copiar la implementación JavaScript anterior.

Las fases construidas hasta ahora son:

- Fase 1: solución y proyectos base.
- Fase 2: presentación WPF, MVVM y pruebas de estados visuales.
- Fase 2B: alineación visual de la portada con la aplicación anterior.
- Fase 3A: modelos y validación de configuración no sensible.
- Fase 3B: contrato y persistencia JSON de configuración no sensible.
- Fase 4: almacenamiento protegido de credenciales PAM.
- Fase 5: generación TOTP RFC 6238 y evaluación de ventana segura.
- Fase 6: reloj confiable calibrado con HTTP `Date` y avance monotónico.
- Fase 7: Edge externo visible y contexto Playwright temporal restringido al origen PAM.
- Fase 8: autenticación PAM simulada con reloj confiable, ventana TOTP y reintento controlado.
- Fase 9: adaptador de autenticación PAM real visible y validación de integración limitada al login.
- Fase 10: sesión autenticada reutilizable y lectura paginada de Favorites sin abrir formularios.
- Fase 11: llenado, verificación y descarte de todos los formularios con envío inaccesible y bloqueado por red.
- Fase 12: autorización ligada al snapshot, envío real verificado y clasificación segura de respuestas PAM.
- Fase 13: lectura paginada de Pending y reconciliación estricta con la solicitud confirmada.
- Fase 14: contratos, validación, fake y adaptador Outlook clásico COM ejecutado en STA.
- Fase 15: saneamiento XHTML, sustitución única de Request ID y validación defensiva previa a COM.
- Fase 16: configuración WPF de credenciales y solicitud, confirmación real y resultados en memoria.
- Fase 16.5: paridad funcional de configuración, pruebas no destructivas y cuenta Outlook desacoplada.
- Fase 16.6: logging seguro de desarrollo con retención local de dos días.
- Fase 16.7: activación tardía de Outlook COM sin dependencia de la PIA administrada.
- Fase 16.8: mensajes operativos, animación bajo demanda y recuperación segura de fallos UI.
- Fase 17: buffer diagnóstico seguro en memoria y exportación ZIP manual y atómica.
- Fase 18: versión nativa, aviso de actualización y publicación portable autocontenida x64.
- Fase 18.5: ventanas compactas, inicio maximizado y parser de hora `hh`/`mm`/`AM-PM`.
- Fase 18.6: importación segura de firmas RTF/HTML con formato e imágenes CID.
- Fase 18.7: pegado HTML prioritario, tablas fieles y editor de correo sin herramientas de construcción manual.
- Fase 18.8: tabla de correo 2×2 insertada manualmente en el cursor y advertencia contra tablas pegadas.
- Fase 18.9: reconciliación de las familias reales de `Application` entre Favorites y Pending.
- Fase 18.10: aviso accionable en la ventana principal para explicar cada bloqueo de `Iniciar automatización`.
- Fase 18.11: resultado consultable bajo demanda, semántica de envío clara y composición Outlook con tipografía y tabla compactas.
- Fase 20.1: catálogo protegido de Favorites con presentación `Application - IP Address` y actualización dinámica.
- Fase 21: acceso interactivo persistente separado del flujo de solicitudes.
- Fase 21.1: preflight de Pending con repetición o cancelación explícitas; el intento WebSocket/CDP quedó sustituido en fase 21.2.
- Fase 21.2: Edge normal mediante Windows UI Automation, confianza manual del certificado y liberación sin cerrar el navegador.
- Fase 21.3: zoom seguro y verificado al 67 % únicamente sobre la ventana PAM autenticada.
- Fase 21.4: contexto temporal `headless` para ocultar la navegación de creación de solicitudes; acceso interactivo visible sin cambios.
- Fase 21.5: confirmación general inmediata antes de preparar y diálogos posteriores reservados a decisiones imprescindibles.
- Fase 21.6: confirmación con hora `AM/PM`, destinatarios `Para`/`CC` explícitos y configuración de solicitud sin selector visible de modo de prueba.
- Fase 21.7: cuadros importantes centralizados como avisos task-modal, topmost y solicitantes de primer plano.
- Fase 21.8: progreso de solicitudes tipado por `Application` y acceso interactivo disponible durante el flujo oculto.
- Fase 21.9: refresco temporal WPF cada minuto y doble validación local alrededor de la confirmación real.
- Fase 21.10: margen mínimo central de 6 minutos y clasificación defensiva de respuestas PAM con JSON no estructurado como objeto.

Los directorios `.git`, `.vs`, `bin` y `obj` contienen metadatos o salidas generadas y se omiten en el árbol.

### Árbol actual del repositorio

```text
PamAutomation\
├── AGENTS.md
├── CODEX_BUILD_SPEC.md
├── README.md
├── LOGIC.md
├── History.md
├── structure.md
├── Fases.md
├── Subagentes.md
├── PAM_NAVIGATION.md
├── .gitignore
├── PamAutomation.slnx
│
├── PamAutomation.App\
│   ├── PamAutomation.App.csproj
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── AssemblyInfo.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── Services\
│   │   ├── ConfigurationOperationResult.cs
│   │   ├── CredentialConfigurationState.cs
│   │   ├── CredentialConfigurationWorkflow.cs
│   │   ├── FlowDocumentHtmlConverter.cs
│   │   ├── ICredentialConfigurationWorkflow.cs
│   │   ├── IConfigurationDialogService.cs
│   │   ├── IUserDialogService.cs
│   │   ├── MailConfigurationEdit.cs
│   │   ├── RequestConfigurationEdit.cs
│   │   ├── RequestStartTimeInput.cs
│   │   ├── RichClipboardHtmlImporter.cs
│   │   ├── UrgentMessageBox.cs
│   │   ├── FlowDocumentTableInserter.cs
│   │   ├── WpfConfigurationDialogService.cs
│   │   ├── WpfPamCertificateTrustPrompt.cs
│   │   └── WpfUserDialogService.cs
│   ├── Views\
│   │   ├── CredentialsWindow.xaml
│   │   ├── CredentialsWindow.xaml.cs
│   │   ├── FavoritesCatalogWindow.xaml
│   │   ├── FavoritesCatalogWindow.xaml.cs
│   │   ├── MailSettingsWindow.xaml
│   │   ├── MailSettingsWindow.xaml.cs
│   │   ├── RequestSettingsWindow.xaml
│   │   └── RequestSettingsWindow.xaml.cs
│   ├── Resources\
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   └── ViewModels\
│       ├── FavoritesCatalogViewModel.cs
│       └── MainWindowViewModel.cs
│
├── PamAutomation.Core\
│   ├── PamAutomation.Core.csproj
│   ├── Interfaces\
│   │   ├── Authentication\
│   │   │   ├── IAsyncDelay.cs
│   │   │   ├── IPamAuthenticatedSessionFactory.cs
│   │   │   ├── IPamInteractiveAccessService.cs
│   │   │   ├── IPamAuthenticationPortal.cs
│   │   │   ├── IPamAuthenticationPortalFactory.cs
│   │   │   ├── PamCredentialSubmissionResult.cs
│   │   │   └── PamTotpSubmissionResult.cs
│   │   ├── Browser\
│   │   │   ├── IPamBrowserSession.cs
│   │   │   └── IPamBrowserSessionFactory.cs
│   │   ├── Configuration\
│   │   │   ├── IAppSettingsStore.cs
│   │   │   ├── IMailSettingsStore.cs
│   │   │   └── IOutlookAccountSettingsStore.cs
│   │   ├── Credentials\
│   │   │   ├── CredentialStoreErrorCode.cs
│   │   │   ├── CredentialStoreException.cs
│   │   │   └── IPamCredentialStore.cs
│   │   ├── Diagnostics\
│   │   │   ├── IDiagnosticEventSource.cs
│   │   │   └── IDiagnosticExportService.cs
│   │   ├── Favorites\
│   │   │   ├── IFavoriteCatalogService.cs
│   │   │   ├── IFavoriteCatalogStore.cs
│   │   │   ├── IPamFavoritesReader.cs
│   │   │   ├── IPamFavoritesPortal.cs
│   │   │   └── IPamFavoritesPortalFactory.cs
│   │   ├── Execution\
│   │   │   └── IPamAutomationExecutionService.cs
│   │   ├── Requests\
│   │   │   ├── IPamPendingPortal.cs
│   │   │   ├── IPamPendingPortalFactory.cs
│   │   │   ├── IPamRequestSubmissionPortal.cs
│   │   │   ├── IPamRequestSubmissionPortalFactory.cs
│   │   │   ├── IPamRequestTestPortal.cs
│   │   │   └── IPamRequestTestPortalFactory.cs
│   │   ├── Outlook\
│   │   │   └── IOutlookClassicClient.cs
│   │   └── Time\
│   │       ├── IClock.cs
│   │       ├── IPamClockFactory.cs
│   │       ├── PamClockErrorCode.cs
│   │       └── PamClockException.cs
│   ├── Authentication\
│   │   ├── PamAuthenticationErrorCode.cs
│   │   ├── PamAuthenticationException.cs
│   │   ├── PamAuthenticationService.cs
│   │   └── TaskAsyncDelay.cs
│   ├── Browser\
│   │   ├── PamBrowserExceptions.cs
│   │   └── PamNavigationResponse.cs
│   ├── Diagnostics\
│   │   ├── DiagnosticExceptionInfo.cs
│   │   ├── DiagnosticExportContext.cs
│   │   └── DiagnosticLogEvent.cs
│   ├── Favorites\
│   │   ├── FavoriteCatalogEntry.cs
│   │   ├── FavoriteCatalogException.cs
│   │   ├── FavoriteCatalogRefreshResult.cs
│   │   ├── FavoriteCatalogService.cs
│   │   ├── FavoriteCatalogSnapshot.cs
│   │   ├── PamFavoriteAccount.cs
│   │   ├── PamFavoritesErrorCode.cs
│   │   ├── PamFavoritesException.cs
│   │   └── PamFavoritesService.cs
│   ├── Execution\
│   │   ├── PamAutomationExecutionService.cs
│   │   ├── PamExecutionMode.cs
│   │   ├── PamExecutionPreparation.cs
│   │   └── PamExecutionResult.cs
│   ├── Models\
│   │   ├── Configuration\
│   │   │   ├── AppSettings.cs
│   │   │   ├── FinalAction.cs
│   │   │   ├── MailSettings.cs
│   │   │   ├── OutlookAccountSettings.cs
│   │   │   └── RequestSettings.cs
│   │   └── Credentials\
│   │       ├── PamCredentials.cs
│   │       └── PamCredentialUpdate.cs
│   ├── Outlook\
│   │   ├── OutlookHtmlComposer.cs
│   │   ├── OutlookHtmlCompositionErrorCode.cs
│   │   ├── OutlookHtmlCompositionException.cs
│   │   ├── OutlookAccount.cs
│   │   ├── OutlookAccountResolutionErrorCode.cs
│   │   ├── OutlookAccountResolutionException.cs
│   │   ├── OutlookDeliveryResult.cs
│   │   ├── OutlookDeliveryStatus.cs
│   │   ├── OutlookExistingMessageStatus.cs
│   │   ├── OutlookInlineImage.cs
│   │   ├── OutlookMessageValidator.cs
│   │   ├── OutlookOperationErrorCode.cs
│   │   ├── OutlookOperationException.cs
│   │   ├── OutlookSendRequest.cs
│   │   ├── OutlookValidationErrorCode.cs
│   │   └── OutlookValidationException.cs
│   ├── Requests\
│   │   ├── PamPendingComparisonResult.cs
│   │   ├── PamPendingErrorCode.cs
│   │   ├── PamPendingException.cs
│   │   ├── PamPendingRequest.cs
│   │   ├── PamPendingRequestMatch.cs
│   │   ├── PamPendingService.cs
│   │   ├── PamRequestAccountResult.cs
│   │   ├── PamRequestConflictCodes.cs
│   │   ├── PamRequestFormData.cs
│   │   ├── PamRequestPortalOutcome.cs
│   │   ├── PamRequestRunAuthorization.cs
│   │   ├── PamRequestRunResult.cs
│   │   ├── PamRequestSubmissionErrorCode.cs
│   │   ├── PamRequestSubmissionException.cs
│   │   ├── PamRequestSubmissionProgress.cs
│   │   ├── PamRequestSubmissionService.cs
│   │   ├── PamRequestSubmissionStatus.cs
│   │   ├── PamRequestTestErrorCode.cs
│   │   ├── PamRequestTestException.cs
│   │   ├── PamRequestTestResult.cs
│   │   ├── PamRequestTestResultIds.cs
│   │   └── PamRequestTestService.cs
│   ├── Totp\
│   │   ├── Base32SecretDecoder.cs
│   │   ├── InvalidBase32SecretException.cs
│   │   ├── TotpCode.cs
│   │   └── TotpGenerator.cs
│   └── Validation\
│       ├── AppSettingsValidator.cs
│       ├── ConfigurationValidationErrorCode.cs
│       ├── ConfigurationValidationFailure.cs
│       ├── ConfigurationValidationResult.cs
│       ├── MailSettingsValidator.cs
│       └── RequestSettingsValidator.cs
│
├── PamAutomation.Browser\
│   ├── PamAutomation.Browser.csproj
│   ├── BrowserAbstractions.cs
│   ├── GlobalUsings.cs
│   ├── IPamCertificateTrustPrompt.cs
│   ├── PamAuthenticationPortal.cs
│   ├── PamFavoritesPagination.cs
│   ├── PamFavoritesPortal.cs
│   ├── PamFavoritesSelectors.cs
│   ├── PamPendingPagination.cs
│   ├── PamPendingPortal.cs
│   ├── PamPendingSelectors.cs
│   ├── PamPendingStartParser.cs
│   ├── PamRequestTestPortal.cs
│   ├── PamRequestTestSelectors.cs
│   ├── PamRequestSubmissionPortal.cs
│   ├── PamSubmissionResponseClassifier.cs
│   ├── PamBrowserSession.cs
│   ├── PamBrowserSessionFactory.cs
│   ├── PamInteractiveAccessService.cs
│   ├── PamInteractiveBrowserSessionFactory.cs
│   ├── PamLoginAlertClassifier.cs
│   ├── PamLoginSelectors.cs
│   ├── PamOriginPolicy.cs
│   ├── PlaywrightManagedPage.Favorites.cs
│   ├── PlaywrightManagedPage.Pending.cs
│   ├── PlaywrightManagedPage.RequestTest.cs
│   ├── PlaywrightManagedPage.RequestSubmission.cs
│   ├── WindowsEdgePamPage.cs
│   └── PlaywrightEngineFactory.cs
│
├── PamAutomation.Infrastructure\
│   ├── PamAutomation.Infrastructure.csproj
│   ├── Configuration\
│   │   ├── AppSettingsDocument.cs
│   │   ├── AppSettingsPersistenceErrorCode.cs
│   │   ├── AppSettingsPersistenceException.cs
│   │   ├── DpapiFavoriteCatalogStore.cs
│   │   ├── DpapiMailSettingsStore.cs
│   │   ├── DpapiOutlookAccountSettingsStore.cs
│   │   └── JsonAppSettingsStore.cs
│   ├── Credentials\
│   │   ├── CredentialManagerEntry.cs
│   │   ├── CredentialNativeMethods.cs
│   │   ├── CredentialPayloadCodec.cs
│   │   ├── IWindowsCredentialApi.cs
│   │   ├── WindowsCredentialApi.cs
│   │   └── WindowsPamCredentialStore.cs
│   ├── Diagnostics\
│   │   ├── DevelopmentFileLoggerFactory.cs
│   │   ├── DiagnosticExportService.cs
│   │   └── RuntimeDiagnosticLoggerFactory.cs
│   └── Time\
│       └── PamHttpDateClockFactory.cs
│
├── PamAutomation.Outlook\
│   ├── PamAutomation.Outlook.csproj
│   ├── AssemblyInfo.cs
│   ├── Properties\PublishProfiles\Portable-x64.pubxml
│   ├── OutlookClassicClient.cs
│   └── Interop\
│       ├── OutlookComAbstractions.cs
│       ├── OutlookComApplicationFactory.cs
│       └── StaExecutor.cs
│
└── PamAutomation.Tests\
    ├── PamAutomation.Tests.csproj
    ├── FavoritesCatalogViewModelTests.cs
    ├── TestExplorerSmokeTests.cs
    ├── MainWindowViewModelTests.cs
    ├── App\
    │   ├── ConfigurationWindowSmokeTests.cs
    │   ├── CredentialConfigurationWorkflowTests.cs
    │   └── FlowDocumentHtmlConverterTests.cs
    ├── Infrastructure\
    │   ├── Configuration\
    │   │   └── DpapiFavoriteCatalogStoreTests.cs
    │   └── Diagnostics\
    │       ├── DevelopmentFileLoggerFactoryTests.cs
    │       ├── DiagnosticExportServiceTests.cs
    │       └── RuntimeDiagnosticLoggerFactoryTests.cs
    ├── Core\
    │   ├── Authentication\
    │   │   └── PamAuthenticationServiceTests.cs
    │   ├── Configuration\
│   │   ├── AppSettingsValidatorTests.cs
    │   │   ├── MailSettingsValidatorTests.cs
    │   │   └── RequestSettingsValidatorTests.cs
    │   ├── Credentials\
    │   │   └── PamCredentialsTests.cs
    │   ├── Favorites\
    │   │   ├── FavoriteCatalogServiceTests.cs
    │   │   └── PamFavoritesServiceTests.cs
    │   ├── Requests\
    │   │   ├── PamPendingServiceTests.cs
    │   │   ├── PamRequestSubmissionServiceTests.cs
    │   │   └── PamRequestTestServiceTests.cs
    │   └── Totp\
    │       └── TotpGeneratorTests.cs
    ├── Browser\
    │   ├── PamAuthenticationPortalTests.cs
    │   ├── PamFavoritesPortalTests.cs
    │   ├── PamPendingPortalTests.cs
    │   ├── PamBrowserSessionFactoryTests.cs
    │   ├── PamInteractiveAccessServiceTests.cs
    │   ├── PamRealFavoritesTests.cs
    │   ├── PamRealPendingMatchTests.cs
    │   ├── PamRealPendingTests.cs
    │   ├── PamRealRequestTestTests.cs
    │   ├── PamRealSingleRequestSubmissionTests.cs
    │   ├── PamRequestSubmissionPortalTests.cs
    │   ├── PamRequestTestPortalTests.cs
    │   └── PamRealAuthenticationTests.cs
    ├── Infrastructure\
    │   ├── Configuration\
    │   │   ├── DpapiMailSettingsStoreTests.cs
    │   │   ├── DpapiOutlookAccountSettingsStoreTests.cs
    │   │   └── JsonAppSettingsStoreTests.cs
    │   ├── Credentials\
    │   │   └── WindowsPamCredentialStoreTests.cs
    │   └── Time\
    │       └── PamHttpDateClockFactoryTests.cs
    └── Outlook\
        ├── OutlookHtmlComposerTests.cs
        └── OutlookClassicClientTests.cs
```

### Documentos de la raíz

| Archivo | Responsabilidad |
|---|---|
| `AGENTS.md` | Reglas obligatorias de trabajo, seguridad, tecnología y validación; también funciona como índice para la consulta documental bajo demanda. |
| `CODEX_BUILD_SPEC.md` | Especificación canónica y fuente principal de requisitos. |
| `README.md` | Estado general, requisitos del entorno y decisiones confirmadas. |
| `LOGIC.md` | Comportamientos, reglas de negocio y contratos previstos. |
| `History.md` | Registro cronológico de decisiones y cambios relevantes. |
| `structure.md` | Inventario físico y arquitectura actual y objetivo. |
| `Fases.md` | Resumen acumulativo de las fases, sus dependencias pendientes y las oportunidades condicionadas de trabajo paralelo. |
| `Subagentes.md` | Protocolo para coordinar un máximo de dos subagentes en unidades verticales independientes, aislar sus cambios e integrar únicamente trabajo validado. |
| `.gitignore` | Exclusiones de Git para salidas, configuración local y artefactos de desarrollo. |
| `PamAutomation.slnx` | Solución .NET que agrupa los seis proyectos en `x64` para operar mediante la CLI `dotnet`. |

## Proyectos actuales

En .NET, cada directorio que contiene un archivo `.csproj` es un proyecto o ensamblado independiente. No todo lo visible en la solución pertenece al mismo módulo.

### `PamAutomation.App`

Proyecto ejecutable WPF para `net10.0-windows`. Es el punto de entrada y la capa de presentación.

Contenido actual:

- `App.xaml` y `App.xaml.cs`: inicio de la aplicación y carga de recursos WPF.
- `MainWindow.xaml`: portada nativa de la aplicación.
- `MainWindow.xaml.cs`: inicialización y ciclo de vida del temporizador de presentación que solicita al ViewModel una revalidación local cada minuto; no contiene reglas de negocio.
- `ViewModels/MainWindowViewModel.cs`: estado de presentación y comandos de la portada mediante `CommunityToolkit.Mvvm`.
- `Views`: diálogos nativos para credenciales PAM y Outlook, solicitud y plantilla de correo, con estados y validación visibles; la solicitud solo expone fecha, hora, duración y razón.
- `Services`: confirmaciones —incluida la presentación `AM/PM`, `Para` y `CC`— y avisos urgentes centralizados, adaptación de diálogos, coordinación no destructiva de credenciales, clasificación segura del resultado de correo de prueba, importación acotada de firmas HTML y conversión bidireccional entre `FlowDocument` y XHTML/CID.
- `Resources/Styles.xaml`: estilos compartidos, incluida la apariencia estática del indicador circular cuya animación controla la ventana únicamente durante operaciones activas.
- `Resources/Colors.xaml`: colores y pinceles compartidos.
- `Resources/Styles.xaml`: estilos y plantillas de controles WPF.
- `AssemblyInfo.cs`: metadatos WPF del ensamblado.

Dependencias actuales:

- `CommunityToolkit.Mvvm` 8.4.2.
- `HtmlAgilityPack` 1.13.0, usado solo para convertir HTML real del portapapeles a un árbol; no decide qué contenido es seguro.
- Referencia a `PamAutomation.Core`.
- Referencia a `PamAutomation.Browser`.
- Referencia a `PamAutomation.Infrastructure`.
- Referencia a `PamAutomation.Outlook`.

Responsabilidad permanente:

- Vistas, diálogos y controles WPF.
- ViewModels y navegación.
- Recursos visuales y accesibilidad.
- Composición de dependencias al iniciar la aplicación.
- Adaptación entre controles WPF y contratos del dominio.

No debe contener:

- Selectores o navegación del portal PAM.
- Algoritmos TOTP.
- Acceso directo a archivos o Credential Manager desde los ViewModels.
- Automatización COM de Outlook.
- Reglas para crear solicitudes.

### `PamAutomation.Core`

Biblioteca para `net10.0`, sin dependencia de WPF o Windows. Actualmente contiene configuración, credenciales, TOTP, autenticación simulada, solicitudes, reconciliación de Pending y contratos de tiempo, navegador y Outlook:

- `Interfaces/Configuration`: contratos `IAppSettingsStore`, `IMailSettingsStore` e `IOutlookAccountSettingsStore` para cargar y guardar configuración sin acoplar Core al sistema de archivos.
- `Interfaces/Credentials`: contrato `IPamCredentialStore` y errores estables para el almacenamiento protegido.
- `Models/Configuration`: `AppSettings`, `RequestSettings`, `MailSettings`, `OutlookAccountSettings` y `FinalAction`.
- `Models/Credentials`: modelo completo de uso exclusivo de autenticación y modelo de actualización con campos opcionales.
- `Validation`: resultado, fallas, códigos estables y validadores de configuración.
- `Totp`: decodificación Base32, generación RFC 6238 y evaluación de la vigencia mínima antes del envío.
- `Interfaces/Time`: reloj confiable, fábrica de calibración y errores estables sin dependencia del sistema.
- `Interfaces/Browser`: contratos de fábrica y sesión que no exponen tipos Playwright.
- `Interfaces/Authentication/IPamInteractiveAccessService`: contrato del acceso manual que entrega al usuario un Edge autenticado sin ejecutar operaciones PAM posteriores.
- `Browser`: respuesta de navegación reducida a URI, estado y HTTP `Date`, junto con errores controlados de inicialización y navegación.
- `Authentication`: servicio de autenticación simulado, espera asincrónica, errores estables y contratos del puerto ligado a una sesión.
- `Interfaces/Favorites` y `Favorites`: lectura y validación de identidades únicas dentro de una sesión autenticada reutilizable, más el catálogo protegido y su diferencia de altas y bajas para colecciones.
- `Interfaces/Requests` y `Requests`: contratos separados para prueba, envío y lectura; coordinación de solicitudes, preflight de coincidencias exactas, línea base de Request ID en memoria y reconciliación estricta de las filas nuevas de Pending.
- `Interfaces/Outlook` y `Outlook`: contrato del cliente clásico, modelos redactados, validaciones, resultados y errores seguros; `OutlookHtmlComposer` sanea XHTML, valida el marcador y compone Request ID por línea.
- `Interfaces/Execution` y `Execution`: preparación confirmable, coordinación completa y resultado exclusivamente en memoria.
- `Interfaces/Diagnostics` y `Diagnostics`: contratos y modelos de lista permitida para capturar y exportar evidencias sin identidades ni secretos.

Los validadores reciben explícitamente la hora actual de Ecuador y no acceden al reloj, sistema de archivos o servicios externos.

Responsabilidad objetivo:

- Modelos y resultados del dominio.
- Interfaces que abstraen navegador, Outlook, reloj, credenciales y persistencia.
- Validación de solicitud y correo.
- Reglas TOTP y su coordinación futura con la hora confiable y la autenticación.
- Reglas de ejecución, confirmación, conflictos y duplicados.

Regla principal: `Core` no debe referenciar ningún otro proyecto de la solución.

### `PamAutomation.Browser`

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa la sesión restringida de Playwright, el login real, Favorites, formularios y Pending de las fases 7, 9, 10, 11, 12 y 13, además del adaptador accesible de Edge normal de fase 21.2:

- `PamBrowserSessionFactory`: crea Edge `headless` mediante el canal externo `msedge` y administra la inicialización segura del flujo automático.
- `PamInteractiveBrowserSessionFactory`: excepción exclusiva de fase 21; crea el adaptador sobre el Edge normal del usuario sin CDP, `userDataDir` ni perfil dedicado.
- `WindowsEdgePamPage`: abre Edge como proceso externo, valida el origen en la barra de direcciones y automatiza solo el login mediante Windows UI Automation. Espera la confianza manual del certificado, ajusta y verifica el zoom PAM al 67 % y nunca cierra el navegador.
- `IPamCertificateTrustPrompt`: separa la detección del certificado en Browser de su explicación visual en WPF.
- `PamInteractiveAccessService`: ejecuta únicamente la autenticación y libera las referencias de automatización sin cerrar el navegador externo.
- `PamBrowserSession`: valida cada navegación y libera página, contexto, navegador y runtime en orden.
- `PamOriginPolicy`: compara esquema, host y puerto efectivo y bloquea solicitudes HTTP(S) externas o malformadas.
- `PlaywrightEngineFactory` y `BrowserAbstractions`: adaptadores internos que encapsulan Microsoft.Playwright y permiten pruebas sin abrir Edge.
- `PamBrowserSession` mide monotónicamente el tiempo de ida y vuelta de cada navegación para calibrar el reloj HTTP.
- `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`: enlazan el contrato de Core únicamente con la página perteneciente a la sesión temporal.
- `PamLoginSelectors`: centraliza roles, nombres accesibles y el texto de rechazo TOTP observados en el portal real.
- `PamLoginAlertClassifier`: evita interpretar el anuncio accesible `Navigation complete.` como rechazo de credenciales.
- `PamFavoritesPortalFactory` y `PamFavoritesPortal`: enlazan la lectura de Favorites con la misma sesión autenticada.
- `PamFavoritesSelectors` y `PamFavoritesPagination`: centralizan Accounts, categorías, grid y paginación accesible.
- `PlaywrightManagedPage.Favorites`: navega, estabiliza las filas y lee todas las páginas mediante locators sin seleccionar cuentas.
- `PamRequestTestPortalFactory` y `PamRequestTestPortal`: enlazan el contrato seguro de formularios con la misma página autenticada.
- `PamRequestTestSelectors`: centraliza acciones, campos y el endpoint de creación que debe permanecer bloqueado.
- `PlaywrightManagedPage.RequestTest`: vuelve a validar cada identidad, llena y comprueba el formulario, lo descarta y aborta cualquier intento de envío.
- `PamRequestSubmissionPortalFactory` y `PamRequestSubmissionPortal`: exponen el contrato real únicamente sobre una sesión temporal propia de Browser.
- `PlaywrightManagedPage.RequestSubmission`: reutiliza el llenado validado, vuelve a comprobar el formulario justo antes del clic y espera el `POST` de creación.
- `PamSubmissionResponseClassifier`: reconoce solo respuestas 2xx y el conflicto de acceso aprobado conocido; rechaza el resto sin propagar payloads.
- `PamPendingPortalFactory` y `PamPendingPortal`: enlazan el lector de Pending con una sesión autenticada existente.
- `PamPendingSelectors`, `PamPendingPagination` y `PamPendingStartParser`: centralizan columnas, paginación y fechas inglesas observadas.
- `PlaywrightManagedPage.Pending`: filtra `Status = Pending`, estabiliza y pagina filas, lee todas las columnas requeridas y captura vistas horizontales de desarrollo.

Dependencia NuGet actual: `Microsoft.Playwright` 1.62.0. Los adaptadores internos se exponen únicamente a `PamAutomation.Tests` mediante `InternalsVisibleTo`.

Responsabilidad objetivo:

- Integración con Microsoft.Playwright para .NET.
- Lanzamiento de Microsoft Edge instalado mediante `Channel = "msedge"`.
- Contexto temporal y aislado por ejecución.
- Restricción de navegación al origen PAM autorizado.
- Autenticación, Favorites, solicitudes y lectura paginada de Pending.
- Selectores del portal centralizados.

No debe administrar configuración local, Outlook ni elementos WPF.

### `PamAutomation.Infrastructure`

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa la persistencia no sensible, correo protegido, catálogo protegido de Favorites, credenciales y reloj HTTP confiable:

- `Configuration/AppSettingsDocument.cs`: representación JSON plana y conversión explícita hacia y desde el modelo de Core.
- `Configuration/JsonAppSettingsStore.cs`: carga desde la ruta privada del usuario y escritura atómica mediante un temporal en la misma carpeta.
- `Configuration/DpapiMailSettingsStore.cs`: cifra el documento privado de correo con DPAPI `CurrentUser` y lo sustituye atómicamente.
- `Configuration/DpapiOutlookAccountSettingsStore.cs`: mantiene la cuenta SMTP clásica en un documento DPAPI independiente, versionado y atómico.
- `Configuration/AppSettingsPersistenceErrorCode.cs` y `AppSettingsPersistenceException.cs`: errores controlados para contenido inválido, esquema incompatible y fallos de lectura o escritura.
- `Credentials/WindowsPamCredentialStore.cs`: implementación del contrato de Core mediante una única credencial genérica del usuario actual.
- `Credentials/CredentialPayloadCodec.cs`: formato binario versionado que mantiene juntas contraseña y semilla TOTP y aplica el límite del blob de Windows.
- `Credentials/WindowsCredentialApi.cs`, `CredentialNativeMethods.cs` y `CredentialManagerEntry.cs`: límite interno sobre `CredReadW`, `CredWriteW`, `CredDeleteW` y `CredFree`, con liberación y limpieza de memoria.
- `Credentials/IWindowsCredentialApi.cs`: punto interno sustituible utilizado exclusivamente por pruebas; no forma parte del dominio público.
- `Time/PamHttpDateClockFactory.cs`: valida HTTP `Date`, calibra el instante recibido y avanza mediante la marca monotónica de `TimeProvider`.

Credential Manager usa directamente las API Win32. Para DPAPI se referencia `System.Security.Cryptography.ProtectedData` 10.0.0. Los tipos internos de Win32 se exponen a `PamAutomation.Tests` únicamente para validar el adaptador sin tocar el almacén real.

Responsabilidad objetivo:

- Windows Credential Manager para usuario, contraseña y semilla TOTP.
- DPAPI `CurrentUser` para contenido sensible de correo.
- Lectura, validación, migración y escritura atómica de configuración.
- Implementaciones del reloj del sistema y zona horaria de Ecuador.
- Detección de dependencias instaladas cuando corresponda.
- Diagnóstico acotado en memoria y exportación explícita, sin persistencia operativa automática en producción.
- `Diagnostics/DevelopmentFileLoggerFactory`: proveedor estructurado activado solo bajo depurador o variable explícita, escribe archivos diarios seguros en `logs` y elimina los que superan dos días.
- `Diagnostics/RuntimeDiagnosticLoggerFactory`: buffer circular de 500 eventos, lista permitida de propiedades y metadatos de excepción sin mensajes o rutas locales.
- `Diagnostics/DiagnosticExportService`: paquete ZIP manual con escritura atómica, limpieza de temporales y contenido estrictamente tipado.

No debe conocer controles WPF, selectores PAM ni objetos COM de Outlook.

### `PamAutomation.Outlook`

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa Outlook clásico mediante COM tardío, sin una dependencia administrada en la PIA de Office:

- `OutlookClassicClient.cs`: resolución exacta, validación descartable, búsqueda de duplicados y envío con estado comprobado cuando es observable.
- `Interop/OutlookComApplicationFactory.cs`: activa el `ProgID` `Outlook.Application` y adapta dinámicamente `Application`, `Account` y `MailItem`; también encapsula consulta de carpetas, huella privada, adjuntos CID y liberación explícita.
- `Interop/StaExecutor.cs`: ejecuta cada operación COM acotada en un hilo STA dedicado.
- `Interop/OutlookComAbstractions.cs`: seam interno sustituido por pruebas sin abrir Outlook.

Responsabilidad objetivo:

- Integración con Outlook clásico mediante Interop/COM.
- Resolución exacta de cuenta por `SmtpAddress`.
- Creación, validación y envío de mensajes.
- Verificación manual de la cuenta mediante correo de prueba.
- Consulta de Enviados, Bandeja de salida y Borradores para evitar duplicados.
- Liberación explícita de referencias COM y ejecución STA.

No debe contener vistas WPF, navegación PAM ni almacenamiento de credenciales PAM.

### `PamAutomation.Tests`

Proyecto MSTest para `net10.0-windows`. Actualmente referencia `PamAutomation.App`, `PamAutomation.Core`, `PamAutomation.Infrastructure`, `PamAutomation.Browser` y `PamAutomation.Outlook`.

Contenido actual:

- `TestExplorerSmokeTests.cs`: comprueba el descubrimiento del proyecto por el runner MSTest.
- `MainWindowViewModelTests.cs`: valida el estado inicial seguro, el bloqueo durante una ejecución y las condiciones del botón de prueba.
- `App/ConfigurationWindowSmokeTests.cs`: carga y renderiza en STA las ventanas de configuración, valida el ciclo de animación bajo demanda y la recuperación de errores sin abrir servicios externos.
- `App/RequestStartTimeInputTests.cs`: valida los límites de `hh`, `mm`, `AM/PM` y su conversión determinista a 24 horas.
- `App/CredentialConfigurationWorkflowTests.cs`: valida guardado y login PAM no destructivo, mensajes seguros, orden verificar-guardar de Outlook y correo de prueba dirigido a la misma cuenta.
- `Outlook/OutlookComApplicationFactoryTests.cs`: valida con objetos falsos la activación tardía y el recorrido `Application → Session → Accounts → MailItem` sin cargar Outlook ni una PIA.
- `App/FlowDocumentHtmlConverterTests.cs`: valida ida y vuelta de formato, tablas, marcador e imágenes CID.
- `App/RichClipboardHtmlImporterTests.cs`: valida firmas HTML con estilos, enlaces, data URI, imagen de respaldo, formatos de portapapeles como string/stream, inserción real en el cursor, tablas con anchos y spans, línea editable posterior y eliminación de contenido activo.
- `App/FlowDocumentTableInserterTests.cs`: valida inserción 2×2 en medio de un párrafo, marcador único movido a la tabla y rechazo de tablas anidadas.
- `Core/Configuration/AppSettingsValidatorTests.cs`: valida valores predeterminados, configuración general y propagación de errores.
- `Core/Configuration/RequestSettingsValidatorTests.cs`: valida fecha, hora, duración y razón de la solicitud.
- `Core/Configuration/MailSettingsValidatorTests.cs`: valida normalización, saneamiento y correspondencia exacta de imágenes CID.
- `Core/Credentials/PamCredentialsTests.cs`: valida campos obligatorios y que los modelos sensibles no expongan valores al convertirse en texto.
- `Core/Totp/TotpGeneratorTests.cs`: valida vectores públicos RFC 6238, Base32, ventanas de 30 segundos, vigencia mínima y redacción del código.
- `Infrastructure/Configuration/JsonAppSettingsStoreTests.cs`: valida formato plano, carga, sustitución atómica, cancelación segura y errores de persistencia sin tocar la configuración real del usuario.
- `Infrastructure/Configuration/DpapiMailSettingsStoreTests.cs`: valida cifrado CurrentUser, ida y vuelta y rechazo de ciphertext corrupto en una carpeta temporal.
- `Infrastructure/Configuration/DpapiOutlookAccountSettingsStoreTests.cs`: valida el documento separado de cuenta Outlook, su cifrado CurrentUser y el rechazo de SMTP inválido o contenido corrupto.
- `Infrastructure/Diagnostics/RuntimeDiagnosticLoggerFactoryTests.cs`: valida capacidad, lista permitida y metadatos de excepción sin información privada.
- `Infrastructure/Diagnostics/DiagnosticExportServiceTests.cs`: valida ZIP, exclusión de identificadores, cancelación y limpieza atómica.
- `Infrastructure/Credentials/WindowsPamCredentialStoreTests.cs`: valida creación, actualización parcial, límites, corrupción, borrado y limpieza de buffers mediante una API de Windows falsa.
- `Infrastructure/Time/PamHttpDateClockFactoryTests.cs`: valida calibración, avance monotónico y errores sin consultar el reloj local.
- `Browser/PamBrowserSessionFactoryTests.cs`: valida Edge `headless`, contexto temporal, origen exacto, respuesta segura, cancelación y liberación con adaptadores falsos.
- `Browser/PamInteractiveAccessServiceTests.cs`: valida que el acceso interactivo libere la automatización sin disponer los recursos del navegador entregado al usuario.
- `Browser/PamAuthenticationPortalTests.cs`: valida el enlace de sesión, el mapeo del portal y la clasificación segura de anuncios sin abrir Edge.
- `Browser/PamFavoritesPortalTests.cs`: valida el adaptador de Favorites y el formato del paginador con dobles.
- `Browser/PamRealFavoritesTests.cs`: integración opt-in de solo lectura que confirma cuentas completas sin mostrar sus valores.
- `Browser/PamRequestTestPortalTests.cs`: valida el enlace del contrato seguro con un doble de página y rechaza sesiones ajenas.
- `Browser/PamRealRequestTestTests.cs`: integración opt-in visible que valida y descarta todos los formularios y exige cero envíos.
- `Browser/PamRequestSubmissionPortalTests.cs`: valida el enlace del contrato real y la clasificación estricta de respuestas sin abrir Edge.
- `Browser/PamRealSingleRequestSubmissionTests.cs`: integración destructiva separada, con opt-in exacto y límite de un solo intento autorizado.
- `Browser/PamPendingPortalTests.cs`: valida enlace de sesión, paginación y parseo temporal sin abrir Edge.
- `Browser/PamRealPendingTests.cs`: integración opt-in exclusivamente de lectura sobre Pending.
- `Browser/PamRealPendingMatchTests.cs`: integración opt-in que crea como máximo una solicitud cuando no existe la solicitud autorizada y reconcilia todos sus campos.
- `Browser/PamRealAuthenticationTests.cs`: integración explícitamente opt-in que lee Credential Manager y valida solo el login visible autorizado.
- `Core/Favorites/PamFavoritesServiceTests.cs`: valida snapshot, identidades, duplicados, errores seguros y cierre de sesión.
- `Core/Requests/PamRequestTestServiceTests.cs`: valida configuración previa, recorrido completo, detención segura, cierre de sesión y resultado invariablemente no enviado.
- `Core/Requests/PamRequestSubmissionServiceTests.cs`: valida autorización por conteo y configuración, resultados, conflictos, fallos parciales, duración técnica y cierre de sesión.
- `Core/Requests/PamPendingServiceTests.cs`: valida lectura, identidad, expiración, descripción, familias y sufijos de Application, detección de repeticiones exactas, exclusión de Request ID preexistentes, múltiples aplicaciones bajo la misma cuenta, ambigüedad y consistencia eventual.
- `Core/Authentication/PamAuthenticationServiceTests.cs`: valida el flujo simulado, RTT, ventana TOTP, reintento único, errores y cancelación.
- `Outlook/OutlookClassicClientTests.cs`: valida cuenta exacta, mensajes temporales, redacción, STA, huellas, duplicados, adjuntos CID, liberación y estados de envío mediante COM falso.
- `Outlook/OutlookHtmlComposerTests.cs`: valida allowlist XHTML, contenido activo, recursos externos, estilos, marcador, Request ID y errores redactados.

Responsabilidad objetivo:

- Pruebas unitarias de Core y ViewModels.
- Dobles de prueba para navegador, archivos, reloj y Outlook.
- Simulaciones del portal sin credenciales reales.
- Pruebas de integración separadas y explícitamente autorizadas.

Las pruebas automatizadas no deben crear solicitudes PAM ni enviar correos.

## Dependencias permitidas

La dirección de dependencias objetivo es:

```text
                         ┌──────────────────────┐
                         │  PamAutomation.App   │
                         │ WPF + MVVM + inicio  │
                         └──────┬────┬────┬─────┘
                                │    │    │
                 ┌──────────────┘    │    └──────────────┐
                 ▼                   ▼                   ▼
       PamAutomation.Browser  PamAutomation.Infrastructure  PamAutomation.Outlook
                 │                   │                   │
                 └───────────────────┼───────────────────┘
                                     ▼
                            PamAutomation.Core
                         modelos + reglas + contratos

PamAutomation.Tests ──► proyectos necesarios para cada prueba
```

Reglas:

- `Core` no depende de ninguna capa externa.
- `Browser`, `Infrastructure` y `Outlook` dependen de contratos definidos en `Core`.
- Estas tres implementaciones no deben depender entre sí.
- `App` ensambla y consume las implementaciones, pero no absorbe sus responsabilidades.
- `Tests` puede referenciar el proyecto concreto que necesite validar.

## Estructura objetivo

Los nombres finales podrán ajustarse al implementar cada fase, pero las responsabilidades y la dirección de dependencias deben conservarse.

```text
PamAutomation\
├── documentación y solución
│   ├── AGENTS.md
│   ├── CODEX_BUILD_SPEC.md
│   ├── README.md
│   ├── LOGIC.md
│   ├── History.md
│   ├── structure.md
│   ├── Subagentes.md
│   ├── PAM_NAVIGATION.md
│   └── PamAutomation.slnx
│
├── PamAutomation.App\
│   ├── Views\
│   │   ├── MainWindow.xaml
│   │   ├── CredentialsView.xaml
│   │   ├── RequestSettingsView.xaml
│   │   ├── MailSettingsView.xaml
│   │   ├── ResultView.xaml
│   │   └── Dialogs\
│   │       ├── RealRunConfirmationDialog.xaml
│   │       ├── ErrorDialog.xaml
│   │       └── TestMailConfirmationDialog.xaml
│   ├── ViewModels\
│   │   ├── MainWindowViewModel.cs
│   │   ├── CredentialsViewModel.cs
│   │   ├── RequestSettingsViewModel.cs
│   │   ├── MailSettingsViewModel.cs
│   │   └── ResultViewModel.cs
│   ├── Resources\
│   │   ├── Colors.xaml
│   │   ├── Styles.xaml
│   │   └── Icons.xaml
│   ├── Navigation\
│   ├── MailEditing\
│   ├── App.xaml
│   └── App.xaml.cs
│
├── PamAutomation.Core\
│   ├── Models\
│   │   ├── Configuration\
│   │   ├── Requests\
│   │   ├── Mail\
│   │   └── Execution\
│   ├── Interfaces\
│   ├── Validation\
│   ├── Totp\
│   ├── BusinessRules\
│   └── Execution\
│
├── PamAutomation.Browser\
│   ├── Sessions\
│   ├── Authentication\
│   ├── Favorites\
│   ├── Requests\
│   ├── Navigation\
│   └── Selectors\
│
├── PamAutomation.Infrastructure\
│   ├── Credentials\
│   ├── Configuration\
│   │   └── Migrations\
│   ├── Files\
│   ├── Time\
│   ├── SystemChecks\
│   └── Diagnostics\
│
├── PamAutomation.Outlook\
│   ├── Accounts\
│   ├── Composition\
│   ├── Delivery\
│   ├── DuplicateDetection\
│   └── Interop\
│
├── PamAutomation.Tests\
│   ├── App\
│   ├── Core\
│   ├── Browser\
│   ├── Infrastructure\
│   ├── Outlook\
│   ├── Fakes\
│   └── Integration\
│
└── PamAutomation.Installer\               futuro, formato por decidir
```

No se deben crear carpetas vacías por adelantado. Cada submódulo aparecerá únicamente cuando una fase implemente una responsabilidad real.

## Ampliación post-migración planificada

La fase 20 añadirá colecciones personalizadas sin convertirlas en una función del portal PAM. La forma física definitiva se decidirá después de la migración, pero deberá respetar estos límites:

- `PamAutomation.Core/Collections`: modelos de colección, claves `System + Application + Account` de cada `Favorites` row, configuración predeterminada, sobrescrituras opcionales y reconciliación con `Favorites`.
- `PamAutomation.Infrastructure/Collections`: documento versionado, protección DPAPI `CurrentUser` y escritura atómica bajo la configuración privada del usuario.
- `PamAutomation.App/Views` y `ViewModels`: listado de colecciones, editor de nombre, selector paginado o filtrable de `Favorites` rows y configuración WPF con hora `AM/PM`.
- `PamAutomation.Browser`: no administrará colecciones; continuará entregando el snapshot vivo de `Favorites` y ejecutando únicamente el subconjunto validado que Core le indique.
- `PamAutomation.Tests`: cubrirá reconciliación, persistencia protegida, selección, conversiones horarias y bloqueo de envíos no autorizados.

Esta sección describe arquitectura prevista y no afirma que esos directorios o módulos existan todavía. El detalle funcional y de navegación está en `PAM_NAVIGATION.md`.

## Flujo objetivo de ejecución

```text
Usuario
  │
  ▼
App / ViewModel
  │ valida entrada mediante Core
  ├──► Infrastructure ──► configuración protegida y credenciales
  ├──► Browser ─────────► Edge externo ──► portal PAM autorizado
  └──► Outlook ─────────► Outlook clásico, solo cuando corresponde
  │
  ▼
Resultado de Core mantenido en memoria
  │
  ▼
Vista de resultados WPF
```

El flujo real deberá coordinarse mediante interfaces de `Core`. La interfaz no llamará directamente a Playwright, Credential Manager, DPAPI o COM.

## Persistencia objetivo fuera del repositorio

La aplicación instalada solo podrá mantener esta configuración privada por usuario:

```text
%LOCALAPPDATA%\PamAutomation\
└── config\
    ├── app-settings.json
    ├── mail-settings.dat
    ├── outlook-account.dat
    ├── favorite-catalog.dat
    └── collections.dat                   futuro, fase 20.2
```

- `app-settings.json`: configuración no secreta, con `schemaVersion` y escritura atómica.
- `mail-settings.dat`: configuración de correo protegida con DPAPI `CurrentUser`.
- `outlook-account.dat`: cuenta SMTP exacta de Outlook clásico, versionada y protegida con DPAPI `CurrentUser`.
- `favorite-catalog.dat`: último snapshot completo de `Favorites`, versionado, atómico y protegido con DPAPI `CurrentUser`; sostiene el estado Obtener/Actualizar y la reconciliación futura.
- `collections.dat`: colecciones post-migración con identidades y razones, versionadas y protegidas con DPAPI `CurrentUser`.
- Usuario, contraseña y semilla TOTP: Windows Credential Manager.
- Resultados, Request ID, conflictos y estado: únicamente en memoria.

Durante desarrollo podrán existir `results`, `logs`, `artifacts` y `temp` en la raíz del repositorio, excluidos de Git y sin secretos. No forman parte de la arquitectura persistente del producto publicado.

### Actualizaciones y publicación portable

- `PamAutomation.Core/Interfaces/Updates/IApplicationUpdateService.cs`: contrato asíncrono y cancelable para consultar una versión publicada.
- `PamAutomation.Core/Updates/ApplicationUpdateResult.cs`: resultado reducido a versión validada y disponibilidad.
- `PamAutomation.Infrastructure/Updates/PublicManifestApplicationUpdateService.cs`: lectura HTTPS independiente del proveedor, limitada a 16 KiB, con JSON estricto, versión numérica y repositorio esperado.
- `PamAutomation.App/Properties/PublishProfiles/Portable-x64.pubxml`: perfil `Release`, `win-x64`, autocontenido, archivo único, sin trimming, AOT, PDB ni archivos auxiliares; permite que Playwright extraiga su driver interno en `%TEMP%`.
- `artifacts/publish/final/PAM-Automation-<versión>/PAM Automation.exe`: salida portable canónica generada e ignorada por Git; contiene .NET y el driver Windows de Playwright, pero no incluye Edge. Las carpetas históricas directamente bajo `artifacts/publish`, como `PAM-Automation-0.0.1` y `win-x64`, son salidas anteriores con símbolos y pueden eliminarse sin afectar el código ni la configuración.

El servicio de actualización depende solo de su interfaz de Core. El ViewModel muestra un banner descartable y desconoce HTTP o GitHub. La aplicación consulta la URL `raw` del manifiesto y exige que este apunte al repositorio GitHub configurado, sin relajar TLS.

## Límites que deben conservarse

- Solo C#, .NET 10, WPF y XAML para la aplicación.
- Playwright para .NET utilizará Edge externo con contexto temporal.
- Outlook clásico se integrará exclusivamente mediante COM.
- No se incluirán JavaScript, Node.js, Python, Java, PowerShell, Electron, WebView2, Selenium ni Chromium.
- La aplicación anterior nunca será una dependencia ni se modificará.
- La lógica de negocio permanecerá fuera del code-behind.
- No se crearán módulos compartidos genéricos sin una responsabilidad concreta.
- No se introducirán dependencias entre capas únicamente para reutilizar código menor.

## Criterio para actualizar este documento

Al terminar cada fase se debe registrar:

- Nuevos proyectos o carpetas creados.
- Responsabilidad de cada módulo nuevo.
- Referencias agregadas o eliminadas entre proyectos.
- Dependencias NuGet relevantes.
- Diferencias entre la estructura planeada y la implementada.
- Elementos objetivo que ya pasaron a estado actual.
