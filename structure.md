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
- Fase 20.2: creación y edición de colecciones con selección exclusiva y razón propia.
- Fase 20.3: autorización de ejecución restringida a las claves exactas de la colección activa.
- Fase 20.4: `All favorites` sincronizada, colección activa obligatoria y configuración temporal separada.
- Fase 20.5: eliminación confirmada de colecciones personalizadas con protección de `All favorites` y reasignación segura de la selección activa.
- Ajuste posterior a fase 20: normalización y persistencia de una hora vencida únicamente durante la inicialización de `MainWindowViewModel`.
- Fase 21: acceso interactivo persistente separado del flujo de solicitudes.
- Fase 21.1: preflight de Pending para detectar solicitudes existentes; su política original de repetición fue sustituida por el bloqueo seguro posterior a fase 25.
- Fase 21.2: Edge normal mediante Windows UI Automation, confianza manual del certificado y liberación sin cerrar el navegador.
- Fase 21.3: zoom seguro y verificado al 67 % únicamente sobre la ventana PAM autenticada.
- Fase 21.4: contexto temporal `headless` para ocultar la navegación de creación de solicitudes; acceso interactivo visible sin cambios.
- Fase 21.5: confirmación general inmediata antes de preparar y diálogos posteriores reservados a decisiones imprescindibles.
- Fase 21.6: confirmación con hora `AM/PM`, destinatarios `Para`/`CC` explícitos y configuración de solicitud sin selector visible de modo de prueba.
- Fase 21.7: cuadros importantes centralizados como avisos task-modal, topmost y solicitantes de primer plano.
- Fase 21.8: progreso de solicitudes tipado por `Application` y acceso interactivo disponible durante el flujo oculto.
- Fase 21.9: refresco temporal WPF cada minuto y doble validación local alrededor de la confirmación real.
- Fase 21.10: margen mínimo central de 6 minutos y clasificación defensiva de respuestas PAM con JSON no estructurado como objeto.
- Fase 24: reconciliación genérica de Pending mediante línea base de Request ID, `System + Account`, atributos exactos del lote y conteos, sin familias de `Application`.
- Fase 24.4: reutilización explícita para correo de un conjunto existente completo y no ambiguo.
- Ajuste posterior a fase 25: bloqueo de toda nueva solicitud idéntica; solo permite reutilizar un conjunto completo para correo o cancelar y cambiar inicio, duración o motivo.

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
├── problemas.md
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
│   │   ├── RequestConfigurationEdit.cs
│   │   ├── RequestStartTimeInput.cs
│   │   ├── RepeatedPendingDecision.cs
│   │   ├── RichClipboardHtmlImporter.cs
│   │   ├── UrgentMessageBox.cs
│   │   ├── FlowDocumentTableInserter.cs
│   │   ├── WpfConfigurationDialogService.cs
│   │   ├── WpfPamCertificateTrustPrompt.cs
│   │   ├── WindowTitleBarAppearance.cs
│   │   └── WpfUserDialogService.cs
│   ├── Views\
│   │   ├── CredentialsWindow.xaml
│   │   ├── CredentialsWindow.xaml.cs
│   │   ├── FavoritesCatalogWindow.xaml
│   │   ├── FavoritesCatalogWindow.xaml.cs
│   │   ├── MailSettingsWindow.xaml
│   │   ├── MailSettingsWindow.xaml.cs
│   │   ├── RequestSettingsWindow.xaml
│   │   ├── RequestSettingsWindow.xaml.cs
│   │   ├── RepeatedPendingDecisionWindow.xaml
│   │   └── RepeatedPendingDecisionWindow.xaml.cs
│   ├── Resources\
│   │   ├── Colors.xaml
│   │   ├── pam-automation-icon.ico
│   │   ├── pam-automation-icon.png
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
│   │   │   ├── IMailSignatureStore.cs
│   │   │   └── IOutlookAccountSettingsStore.cs
│   │   ├── Credentials\
│   │   │   ├── CredentialStoreErrorCode.cs
│   │   │   ├── CredentialStoreException.cs
│   │   │   └── IPamCredentialStore.cs
│   │   ├── Diagnostics\
│   │   │   ├── IDiagnosticEventSource.cs
│   │   │   ├── IDiagnosticExportService.cs
│   │   │   └── IPendingReconciliationDiagnosticStore.cs
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
│   │   ├── DiagnosticLogEvent.cs
│   │   └── PendingReconciliationDiagnostic.cs
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
│   │   │   ├── CollectionMailSettings.cs
│   │   │   ├── MailSignature.cs
│   │   │   ├── OutlookAccountSettings.cs
│   │   │   └── RequestSettings.cs
│   │   └── Credentials\
│   │       ├── PamCredentials.cs
│   │       └── PamCredentialUpdate.cs
│   ├── Outlook\
│   │   ├── CollectionMailComposer.cs
│   │   ├── OutlookHtmlComposer.cs
│   │   ├── OutlookHtmlCompositionErrorCode.cs
│   │   ├── OutlookHtmlCompositionException.cs
│   │   ├── OutlookAccount.cs
│   │   ├── OutlookAccountResolutionErrorCode.cs
│   │   ├── OutlookAccountResolutionException.cs
│   │   ├── OutlookDeliveryResult.cs
│   │   ├── OutlookDeliveryConfirmationService.cs
│   │   ├── OutlookDeliveryStatus.cs
│   │   ├── OutlookExistingMessageStatus.cs
│   │   ├── OutlookInlineImage.cs
│   │   ├── OutlookMessageValidator.cs
│   │   ├── OutlookOperationErrorCode.cs
│   │   ├── OutlookOperationException.cs
│   │   ├── OutlookSendRequest.cs
│   │   ├── OutlookValidationErrorCode.cs
│   │   └── OutlookValidationException.cs
│   ├── Resilience\
│   │   └── PamOperationTimingPolicy.cs
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
│   │   ├── PamRequestFormVerificationException.cs
│   │   ├── PamRequestPortalOutcome.cs
│   │   ├── PamRequestRunAuthorization.cs
│   │   ├── PamRequestRunResult.cs
│   │   ├── PamRequestSubmissionErrorCode.cs
│   │   ├── PamRequestSubmissionException.cs
│   │   ├── PamRequestSubmissionProgress.cs
│   │   ├── PamRequestSubmissionService.cs
│   │   ├── PamRequestSubmissionStatus.cs
│   │   ├── PamRequestTestErrorCode.cs
│   │   ├── PamCalendarSelectionException.cs
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
│   ├── PendingGridSnapshot.cs
│   ├── PamRequestTestPortal.cs
│   ├── PamRequestTestSelectors.cs
│   ├── PamCalendarDateSelector.cs
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
│   ├── PlaywrightManagedPage.RequestNavigation.cs
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
│   │   ├── DpapiMailSignatureStore.cs
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
│   │   ├── InMemoryPendingReconciliationDiagnosticStore.cs
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
    │   ├── PamPendingAtomicGridLoadTests.cs
    │   ├── PamAuthenticationPortalTests.cs
    │   ├── PamFavoritesPortalTests.cs
    │   ├── PamPendingPortalTests.cs
    │   ├── PamBrowserSessionFactoryTests.cs
    │   ├── PamInteractiveAccessServiceTests.cs
    │   ├── PamRealFavoritesTests.cs
    │   ├── PamRealPendingMatchTests.cs
    │   ├── PamRealPendingTests.cs
    │   ├── PamRealRequestTestTests.cs
    │   ├── PamFavoritesRequestNavigationTests.cs
    │   ├── PamRealFavoritesNavigationTests.cs
    │   ├── PamCalendarDateSelectorTests.cs
    │   ├── PamRealCalendarSubmissionTests.cs
    │   ├── PamRealSingleRequestSubmissionTests.cs
    │   ├── PamRequestSubmissionPortalTests.cs
    │   ├── PamRequestTestPortalTests.cs
    │   └── PamRealAuthenticationTests.cs
    ├── Infrastructure\
    │   ├── Configuration\
    │   │   ├── DpapiFavoriteCatalogStoreTests.cs
    │   │   ├── DpapiMailSettingsStoreTests.cs
    │   │   ├── DpapiOutlookAccountSettingsStoreTests.cs
    │   │   └── JsonAppSettingsStoreTests.cs
    │   ├── Credentials\
    │   │   └── WindowsPamCredentialStoreTests.cs
    │   ├── Diagnostics\
    │   │   ├── DevelopmentFileLoggerFactoryTests.cs
    │   │   ├── DiagnosticExportServiceTests.cs
    │   │   ├── InMemoryPendingReconciliationDiagnosticStoreTests.cs
    │   │   └── RuntimeDiagnosticLoggerFactoryTests.cs
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
| `problemas.md` | Traspaso técnico de incidentes, riesgos y mejoras de diagnóstico: evidencia, hipótesis, clases implicadas, propuestas y criterios de aceptación. Distingue pendientes de correcciones verificadas. |
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
- `Views`: diálogos nativos para credenciales PAM y Outlook, solicitud, plantilla de correo, catálogo de colecciones y editor de colección; `Editar solicitud` expone fecha, hora y duración, y cada colección mantiene su razón.
- `ViewModels/FavoritesCatalogViewModel`, `FavoriteCollectionItemViewModel` y `FavoriteSelectionItemViewModel`: catálogo actualizado, varias colecciones, selección exclusiva y checklist de `Favorites` sin exponer claves internas en la presentación.
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
- `Interfaces/Favorites` y `Favorites`: lectura y validación de identidades únicas, catálogo protegido, claves exactas, colecciones nombradas, selección activa obligatoria y sincronización de `All favorites` contra `Favorites`.
- `Interfaces/Requests` y `Requests`: contratos separados para prueba, envío y lectura; coordinación de solicitudes, preflight de coincidencias exactas, línea base de Request ID en memoria y reconciliación estricta de las filas nuevas de Pending. `PamPendingService` agrupa como multiconjunto por `System + Account`, valida inicio, expiración y descripción y no depende de `Application`; ante timeouts del preflight o posteriores al envío recarga Pending y puede reemplazar únicamente su sesión temporal de lectura, sin reenviar solicitudes. `PamAutomationExecutionService` impide que IDs de la línea base alcancen accidentalmente resultados o correo y ofrece una ruta separada, explícita y completa para reutilizarlos sin crear solicitudes. Esa ruta puede reenviar deliberadamente una huella de correo existente; las demás conservan la deduplicación.
- `Interfaces/Outlook` y `Outlook`: contrato del cliente clásico, modelos redactados, validaciones, resultados y errores seguros; `OutlookHtmlComposer` sanea XHTML, valida el marcador y compone Request ID por línea.
- `Resilience/PamOperationTimingPolicy`: fuente única de límites, intervalos y cantidades de observación adaptativa para PAM, Pending, Edge interactivo y Outlook; no autoriza reintentos de acciones irreversibles.
- `Interfaces/Execution` y `Execution`: preparación confirmable, selección opcional de colección, coordinación completa y resultado exclusivamente en memoria.
- `Interfaces/Diagnostics` y `Diagnostics`: contratos y modelos para el buffer seguro habitual y para una traza detallada de Pending autorizada explícitamente. Esta última puede contener identidades operativas, pero nunca secretos de autenticación, y solo se materializa mediante exportación manual.

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
- `WindowsEdgePamPage`: abre Edge como proceso externo, valida el origen en la barra de direcciones y automatiza solo el login mediante Windows UI Automation. Observa adaptativamente el arranque y cada respuesta, espera la confianza manual del certificado, ajusta y verifica el zoom PAM al 67 % y nunca cierra el navegador.
- `IPamCertificateTrustPrompt`: separa la detección del certificado en Browser de su explicación visual en WPF.
- `PamInteractiveAccessService`: ejecuta únicamente la autenticación y libera las referencias de automatización sin cerrar el navegador externo.
- `PamBrowserSession`: valida cada navegación y libera página, contexto, navegador y runtime en orden.
- `PamOriginPolicy`: compara esquema, host y puerto efectivo y bloquea solicitudes HTTP(S) externas o malformadas.
- `PlaywrightEngineFactory` y `BrowserAbstractions`: adaptadores internos que encapsulan Microsoft.Playwright y permiten pruebas sin abrir Edge. Aplican a páginas y navegaciones el límite central de 60 segundos con sondeo inmediato; la observación de credenciales trata como transitorio que PAM reemplace una alerta entre su comprobación de visibilidad y la lectura del texto. El lector paginado de Pending aplica la misma disciplina a filas que están renderizándose, pero exige dos snapshots completos e idénticos antes de aceptarlas.
- `PamBrowserSession` mide monotónicamente el tiempo de ida y vuelta de cada navegación para calibrar el reloj HTTP.
- `PamAuthenticationPortalFactory` y `PamAuthenticationPortal`: enlazan el contrato de Core únicamente con la página perteneciente a la sesión temporal.
- `PamLoginSelectors`: centraliza roles, nombres accesibles y el texto de rechazo TOTP observados en el portal real.
- `PamLoginAlertClassifier`: evita interpretar el anuncio accesible `Navigation complete.` como rechazo de credenciales.
- `PamFavoritesPortalFactory` y `PamFavoritesPortal`: enlazan la lectura de Favorites con la misma sesión autenticada.
- `PamFavoritesSelectors` y `PamFavoritesPagination`: centralizan Accounts, categorías, grid y paginación accesible.
- `PlaywrightManagedPage.Favorites`: navega, estabiliza las filas y lee todas las páginas mediante locators sin seleccionar cuentas.
- `PamRequestTestPortalFactory` y `PamRequestTestPortal`: enlazan el contrato seguro de formularios con la misma página autenticada.
- `PamRequestTestSelectors`: centraliza acciones, campos y el endpoint de creación que debe permanecer bloqueado.
- `PamCalendarDateSelector`: selecciona fechas mediante la cabecera nativa de mes/año y una grilla coherente; elige directamente la opción de mes y usa flechas para cambios de año. Confirma el valor retenido. Comparte los presupuestos de `PamOperationTimingPolicy` y falla con metadata tipada de `PamCalendarSelectionException`, exportada sin texto libre del portal.
- `PlaywrightManagedPage.RequestNavigation`: preparación compartida por prueba y producción; reutiliza Favorites seleccionado, busca desde la página actual, resuelve Access por identidad completa y permite una recuperación anterior al llenado. Los límites residen en PamOperationTimingPolicy.
- `PlaywrightManagedPage.RequestTest`: usa la preparación compartida, llena el formulario y espera adaptativamente que todos sus valores se estabilicen antes de comprobarlo; captura una observación diagnóstica tipada si falla, después lo descarta y aborta cualquier intento de envío.
- `PamRequestSubmissionPortalFactory` y `PamRequestSubmissionPortal`: exponen el contrato real únicamente sobre una sesión temporal propia de Browser.
- `PlaywrightManagedPage.RequestSubmission`: reutiliza el llenado validado, vuelve a comprobar el formulario justo antes del clic y espera el `POST` de creación.
- `PamSubmissionResponseClassifier`: reconoce solo respuestas 2xx y el conflicto de acceso aprobado conocido; rechaza el resto sin propagar payloads.
- `PamPendingPortalFactory` y `PamPendingPortal`: enlazan el lector de Pending con una sesión autenticada existente.
- `PamPendingSelectors`, `PamPendingPagination` y `PamPendingStartParser`: centralizan identificadores de columnas, selectores, filas por página, paginación y fechas inglesas observadas.
- `PendingGridSnapshot`: DTO interno y no persistente para transportar en una sola operación todas las celdas visibles desde el DOM de PAM hacia C#.
- `PlaywrightManagedPage.Pending`: espera la carga completa de `Status = Pending`, selecciona la mayor cantidad de filas disponible, conserva paginación de respaldo y captura cada página de forma masiva y atómica. C# rechaza columnas ausentes o repetidas, valores obligatorios vacíos, fechas o estados inválidos, exige dos snapshots completos e iguales y mantiene las capturas horizontales de desarrollo.

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

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa la persistencia no sensible, correo protegido, catálogo y colecciones protegidas de Favorites, credenciales y reloj HTTP confiable:

- `Configuration/AppSettingsDocument.cs`: representación JSON plana y conversión explícita hacia y desde el modelo de Core.
- `Configuration/JsonAppSettingsStore.cs`: carga desde la ruta privada del usuario y escritura atómica mediante un temporal en la misma carpeta.
- `Configuration/DpapiMailSettingsStore.cs`: cifra el documento privado de correo con DPAPI `CurrentUser` y lo sustituye atómicamente.
- `Configuration/DpapiOutlookAccountSettingsStore.cs`: mantiene la cuenta SMTP clásica en un documento DPAPI independiente, versionado y atómico.
- `Configuration/DpapiFavoriteCatalogStore.cs`: conserva el último catálogo completo de `Favorites` con metadata `IP Address`.
- `Configuration/DpapiFavoriteCollectionStore.cs`: conserva varias colecciones, sus razones, selecciones y colección activa mediante DPAPI `CurrentUser`; usa esquema 2, migra el esquema 1 y realiza reemplazo atómico.
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
- `Diagnostics/RuntimeDiagnosticLoggerFactory`: buffer circular de 500 eventos, lista permitida de propiedades y metadatos de excepción sin mensajes o rutas locales; extrae valores acotados de fallos tipados del formulario, sin texto libre del motivo ni secretos de autenticación.
- `Diagnostics/InMemoryPendingReconciliationDiagnosticStore`: conserva defensivamente una sola traza detallada por proceso, incluidos los intentos de recuperación de lectura, y nunca escribe archivos por sí mismo.
- `Diagnostics/DiagnosticExportService`: paquete ZIP manual con escritura atómica, limpieza de temporales, versión exacta de compilación y `pending-reconciliation.json` opcional.

No debe conocer controles WPF, selectores PAM ni objetos COM de Outlook.

### `PamAutomation.Outlook`

Biblioteca para `net10.0-windows`. Referencia `PamAutomation.Core` e implementa Outlook clásico mediante COM tardío, sin una dependencia administrada en la PIA de Office:

- `OutlookClassicClient.cs`: resolución exacta, validación descartable, búsqueda de duplicados y envío con estado comprobado cuando es observable.
- `Interop/OutlookComApplicationFactory.cs`: conecta primero con una instancia activa mediante ROT, activa el `ProgID` `Outlook.Application` y recupera `0x80080005` iniciando la aplicación registrada por Windows sin una ruta fija y observando su disponibilidad una vez por segundo dentro de un límite. También adapta dinámicamente `Application`, `Account` y `MailItem`, y encapsula consulta de carpetas, huella privada, adjuntos CID y liberación explícita.
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
- `MainWindowViewModelTests.cs`: valida el estado inicial seguro, el bloqueo durante una ejecución, las decisiones sobre Pending y la confirmación visible del correo explícito.
- `Core/Execution/PamAutomationExecutionServiceEmailTests.cs`: demuestra que una elección explícita con IDs existentes crea un correo nuevo aunque la huella ya haya sido enviada, sin debilitar la ruta ordinaria.
- `App/ConfigurationWindowSmokeTests.cs`: carga y renderiza en STA las ventanas de configuración, valida el ciclo de animación bajo demanda y la recuperación de errores sin abrir servicios externos.
- `App/RequestStartTimeInputTests.cs`: valida los límites de `hh`, `mm`, `AM/PM` y su conversión determinista a 24 horas.
- `App/CredentialConfigurationWorkflowTests.cs`: valida guardado y login PAM no destructivo, mensajes seguros, orden verificar-guardar de Outlook y correo de prueba dirigido a la misma cuenta.
- `Outlook/OutlookComApplicationFactoryTests.cs`: valida con objetos falsos la activación tardía y el recorrido `Application → Session → Accounts → MailItem` sin cargar Outlook ni una PIA.
- `Core/Resilience/PamOperationTimingPolicyTests.cs`: comprueba que los límites adaptativos sean positivos, acotados y con múltiples observaciones sin imponer una espera mínima.
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
- `Infrastructure/Diagnostics/DiagnosticExportServiceTests.cs`: valida ZIP básico, detalle Pending autorizado, manifiesto versionado, cancelación y limpieza atómica.
- `Infrastructure/Diagnostics/InMemoryPendingReconciliationDiagnosticStoreTests.cs`: valida decisiones, comparaciones, recuperaciones, errores y copias defensivas de la traza temporal.
- `Infrastructure/Credentials/WindowsPamCredentialStoreTests.cs`: valida creación, actualización parcial, límites, corrupción, borrado y limpieza de buffers mediante una API de Windows falsa.
- `Infrastructure/Time/PamHttpDateClockFactoryTests.cs`: valida calibración, avance monotónico y errores sin consultar el reloj local.
- `Browser/PamBrowserSessionFactoryTests.cs`: valida Edge `headless`, contexto temporal, origen exacto, respuesta segura, cancelación y liberación con adaptadores falsos.
- `Browser/PamInteractiveAccessServiceTests.cs`: valida que el acceso interactivo libere la automatización sin disponer los recursos del navegador entregado al usuario.
- `Browser/PamAuthenticationPortalTests.cs`: valida el enlace de sesión, el mapeo del portal y la clasificación segura de anuncios sin abrir Edge.
- `Browser/PamFavoritesPortalTests.cs`: valida el adaptador de Favorites y el formato del paginador con dobles.
- `Browser/PamRealFavoritesTests.cs`: integración opt-in de solo lectura que confirma cuentas completas sin mostrar sus valores.
- `Browser/PamRequestTestPortalTests.cs`: valida el enlace del contrato seguro con un doble de página y rechaza sesiones ajenas.
- `Browser/PamFavoritesRequestNavigationTests.cs`: regresiones locales con Edge visible para reutilización, recuperación, paginación, identidad exacta, reordenación, duplicados, ausencia y cancelación; bloquea cualquier POST.
- `Browser/PamRealFavoritesNavigationTests.cs`: integración autorizada con formularios reales descartados; comprueba cero recargas innecesarias de Accounts, retorno desde Requests y cero intentos de creación.
- `Browser/PamCalendarDateSelectorTests.cs`: regresiones opt-in con Edge visible y respuestas locales para cambios de mes/año, selector nativo con grilla parcial o atrasada, bisiestos, repintado lento, calendarios ocultos, fechas no disponibles, valores no retenidos y cancelación. `PamRealRequestTestTests` recorre todos los accesos en modo visible y headless, con creación bloqueada.
- `Browser/PamRealCalendarSubmissionTests.cs`: prueba mutante separada y opt-in; exige autorización expresa para hasta tres solicitudes de cinco minutos, comprueba Pending antes de enviar, limita los intentos y reconcilia sus IDs sin repetir Submit ni usar Outlook.
- `Browser/PamRealRequestTestTests.cs`: integración opt-in visible que valida y descarta todos los formularios y exige cero envíos.
- `Browser/PamRequestSubmissionPortalTests.cs`: valida el enlace del contrato real y la clasificación estricta de respuestas sin abrir Edge.
- `Browser/PamRealSingleRequestSubmissionTests.cs`: integración destructiva separada, con opt-in exacto y límite de un solo intento autorizado.
- `Browser/PamPendingPortalTests.cs`: valida enlace de sesión, paginación, parseo temporal y conversión estricta de snapshots masivos de Pending sin abrir Edge.
- `Browser/PamPendingAtomicGridLoadTests.cs`: prueba snapshots de 30, 60, 100 y 200 filas, una agregación paginada de 200 + 50 y una integración local opt-in que ejecuta la captura masiva real sobre Edge sin contactar PAM.
- `Browser/PamRealPendingTests.cs`: integración opt-in exclusivamente de lectura sobre Pending.
- `Browser/PamRealPendingMatchTests.cs`: integración opt-in que crea como máximo una solicitud cuando no existe la solicitud autorizada y reconcilia todos sus campos.
- `Browser/PamRealAuthenticationTests.cs`: integración explícitamente opt-in que lee Credential Manager y valida solo el login visible autorizado.
- `Core/Favorites/PamFavoritesServiceTests.cs`: valida snapshot, identidades, duplicados, errores seguros y cierre de sesión.
- `Core/Requests/PamRequestTestServiceTests.cs`: valida configuración previa, recorrido completo, detención segura, cierre de sesión y resultado invariablemente no enviado.
- `Core/Requests/PamRequestSubmissionServiceTests.cs`: valida autorización por conteo y configuración, resultados, conflictos, fallos parciales, duración técnica y cierre de sesión.
- `Core/Requests/PamPendingServiceTests.cs`: valida lectura, identidad estable, expiración, descripción, aplicaciones arbitrarias, detección de repeticiones exactas, exclusión de Request ID preexistentes, conteos repetidos bajo `System + Account`, ambigüedad, consistencia eventual, recarga de Pending, reconstrucción acotada de la sesión de lectura y preservación de la línea base.
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

## Ampliación post-migración implementada

La fase 20 incorpora colecciones personalizadas sin convertirlas en una función del portal PAM. Su implementación respeta estos límites:

- `PamAutomation.Core/Favorites`: modelos de colección, claves `System + Application + Account`, razón propia, colección activa y reconciliación con `Favorites`.
- `PamAutomation.Infrastructure/Configuration`: documento versionado, protección DPAPI `CurrentUser` y escritura atómica bajo la configuración privada del usuario.
- `PamAutomation.App/Views` y `ViewModels`: listado de colecciones, editor de nombre/razón, checklist de `Favorites` rows y configuración temporal común con hora `AM/PM`.
- `PamAutomation.Browser`: no administrará colecciones; continuará entregando el snapshot vivo de `Favorites` y ejecutando únicamente el subconjunto validado que Core le indique.
- `PamAutomation.Tests`: cubre reconciliación, persistencia protegida, selección exclusiva, presentación horaria y bloqueo de envíos no autorizados.

El detalle funcional y de navegación está en `PAM_NAVIGATION.md`.

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
    └── favorite-collections.dat
```

- `app-settings.json`: configuración no secreta, con `schemaVersion` y escritura atómica.
- `mail-settings.dat`: configuración de correo protegida con DPAPI `CurrentUser`.
- `outlook-account.dat`: cuenta SMTP exacta de Outlook clásico, versionada y protegida con DPAPI `CurrentUser`.
- `favorite-catalog.dat`: último snapshot completo de `Favorites`, versionado, atómico y protegido con DPAPI `CurrentUser`; sostiene el estado Obtener/Actualizar y la reconciliación de colecciones.
- `favorite-collections.dat`: colecciones con identidades, razones y selección activa, versionadas y protegidas con DPAPI `CurrentUser`.
- Usuario, contraseña y semilla TOTP: Windows Credential Manager.
- Resultados, Request ID, conflictos y estado: únicamente en memoria.

Durante desarrollo podrán existir `results`, `logs`, `artifacts` y `temp` en la raíz del repositorio, excluidos de Git y sin secretos. No forman parte de la arquitectura persistente del producto publicado.

### Actualizaciones y publicación portable

- `PamAutomation.Core/Interfaces/Updates/IApplicationUpdateService.cs`: contrato asíncrono y cancelable para consultar una versión publicada.
- `PamAutomation.Core/Updates/ApplicationUpdateResult.cs`: resultado reducido a versión validada y disponibilidad.
- `PamAutomation.Core/Outlook/OutlookDeliveryConfirmationService.cs`: observación posterior al envío, con espera asíncrona acotada y reintentos de lectura no destructivos; no reenvía ni modifica Bandeja de salida.
- `PamAutomation.Infrastructure/Updates/PublicManifestApplicationUpdateService.cs`: lectura HTTPS independiente del proveedor, limitada a 16 KiB, con JSON estricto, versión numérica y repositorio esperado.
- `PamAutomation.App/Properties/PublishProfiles/Portable-x64.pubxml`: perfil `Release`, `win-x64`, autocontenido y multifichero, sin trimming, AOT, PDB ni extracción de bundle durante el arranque.
- `MainWindowViewModel.BeginInitialization`: estado visual seguro que informa preparación y desactiva acciones hasta terminar la carga local.
- `App.OnStartup`: registra hitos de rendimiento seguros y difiere la consulta de actualización hasta que la aplicación ya está utilizable.
- `artifacts/publish/final/PAM-Automation-<versión>/`: salida portable autocontenida generada e ignorada por Git; contiene el EXE, .NET y el driver Windows de Playwright, pero no incluye Edge.
- `artifacts/publish/final/PAM-Automation-<versión>-win-x64.zip`: único paquete de distribución. Conserva la carpeta base para impedir que el usuario separe el EXE de sus dependencias.

El servicio de actualización depende solo de su interfaz de Core. El ViewModel muestra un banner descartable y desconoce HTTP o GitHub. La aplicación consulta la URL `raw` del manifiesto y exige que este apunte al repositorio GitHub configurado, sin relajar TLS.

### Estructura de la fase 23

La fase 23 no crea perfiles completos ni replica los almacenes de configuración. Las dos primeras entregas mantienen la configuración actual bajo `config` y añaden responsabilidades pequeñas para identidades PAM:

- `PamAutomation.Core/Accounts`: `PamAccountId`, `PamAccountSummary`, `PamAccountRegistryState`, `PamAccountStatus` y las excepciones/códigos del dominio. Define GUID, nombre validado, rol principal/secundario, límite de cinco e invariantes de ciclo de vida.
- `PamAutomation.Core/Interfaces/Accounts`: `IPamAccountRegistryStore` e `IPamAccountService` separan persistencia y operaciones de aplicación.
- `PamAutomation.Infrastructure/Accounts/DpapiPamAccountRegistryStore.cs`: persiste el índice no sensible con esquema versionado, DPAPI CurrentUser y reemplazo atómico en `%LOCALAPPDATA%\PamAutomation\config\pam-accounts.dat`.
- `PamAutomation.Infrastructure/Accounts/PamAccountService.cs`: coordina el índice con Windows Credential Manager, valida TOTP, aplica actualizaciones parciales y ejecuta rollback ante fallos.
- `PamAutomation.Infrastructure/Credentials/WindowsPamCredentialStore.cs`: conserva su destino predeterminado para el principal y ahora admite destinos explícitos aislados para cuentas secundarias.
- `PamAutomation.App/Views/PamAccountNameWindow.*`: creación obligatoria del principal en el primer inicio y edición posterior de su nombre.
- `PamAutomation.App/Views/PamAccountsWindow.*`: administración `Usuarios PAM`, límite de cuentas y protección del rol principal. Su plantilla declara como unidireccionales los textos calculados de rol y credenciales para impedir escrituras de WPF sobre propiedades de solo lectura, y reemplaza el contenedor visual de selección por una presentación transparente que conserva operativos los botones internos.
- `PamAutomation.App/Views/PamSecondaryAccountWindow.*`: alta y edición de una cuenta secundaria sin volver a mostrar secretos.
- `PamAutomation.App/Views/PamAccountSelectionWindow.*`: selector modal para una sola apertura interactiva; muestra estado y rol, y bloquea identidades sin credenciales.
- `PamAutomation.App/Views/AboutWindow.*`: diálogo informativo local con versión del ensamblado, autor y cierre; no depende de servicios operativos.
- `PamAutomation.App/ViewModels/MainWindowViewModel.cs`: calcula por separado la disponibilidad del acceso interactivo, expone nombre e inicial del principal para la cabecera, solicita una identidad y entrega únicamente su credencial a `IPamInteractiveAccessService`. La automatización conserva el almacén principal.
- `PamAutomation.App/App.xaml.cs`: bloquea la portada hasta que exista principal y conserva intacta la configuración funcional existente.
- `PamAutomation.App/Services/PrincipalIdentityBootstrapper.cs`: coordina de forma idempotente la detección o creación inicial del principal y representa explícitamente creación, estado existente o cancelación.
- `PamAutomation.Tests/Core/Accounts` y `PamAutomation.Tests/Infrastructure/Accounts`: dominio, DPAPI, aislamiento de destinos y rollback. `ConfigurationWindowSmokeTests` valida además la carga de las ventanas nuevas.
- `PamAutomation.Tests/App/PrincipalIdentityBootstrapperTests.cs`: aceptación del arranque existente, primera creación, cancelación y carrera de creación concurrente.

El índice almacena únicamente GUID, nombre y rol. Las credenciales permanecen en destinos separados de Credential Manager: la cuenta principal reutiliza `PamAutomation/PamCredentials/v1` y las secundarias usan `PamAutomation/Accounts/<guid>/PamCredentials/v1`. `IPamAccountService.LoadCredentialsAsync` resuelve por GUID una credencial solo después de la confirmación del selector interactivo.

El diseño anterior de perfiles aislados y archivos `.pamprofile` queda descartado. No se moverán `app-settings.json`, correo, Outlook, Favorites o colecciones a ámbitos por identidad.

## Recuperación integral de Pending

- `Core/Requests/PamPendingService.cs`: comparte la recuperación autenticada de solo lectura entre preflight y reconciliación, conserva la línea base y excluye errores permanentes y autenticación del reintento.
- `Browser/PlaywrightManagedPage.Pending.cs`: clasifica cambios del grid como `SnapshotUnstable`; una fila todavía inválida al finalizar su ventana local permanece bloqueante como `InvalidRequest`.
- `Core/Execution/PamAutomationExecutionService.cs`: inicia la traza antes del preflight y registra la línea base completa después de recuperarla, sin repetir la inicialización ni perder su historial.
- `Core/Diagnostics/PendingReconciliationDiagnostic.cs` e `Interfaces/Diagnostics/IPendingReconciliationDiagnosticStore.cs`: etapa, línea base nullable, lecturas y recuperaciones por sesión en el detalle de esquema 3.
- `Infrastructure/Diagnostics/InMemoryPendingReconciliationDiagnosticStore.cs`: actualización sincronizada y defensiva de la línea base y del historial. El exportador existente incluye la traza aunque no se haya obtenido baseline.
- `Tests/Core/Execution/PamAutomationExecutionServicePendingTests.cs`: coordinación simulada, un único envío a pesar de reconstrucciones en ambas etapas, exclusión del ID anterior, exportación del preflight fallido y privacidad del log.
- `Tests/Core/Requests/PamPendingServiceTests.cs` y `Tests/Infrastructure/Diagnostics/InMemoryPendingReconciliationDiagnosticStoreTests.cs`: límites, cancelación, clasificación, autenticación sin reintento y copias defensivas.

No se añaden proyectos, paquetes NuGet ni dependencias entre capas.

### Integridad de Pending al cambiar de página

- `PamAutomation.Browser/PamPendingItemCount.cs`: parser C# del contador simple y del rango visible con total; rechaza rangos incoherentes.
- `PamAutomation.Browser/PlaywrightManagedPage.Pending.cs`: sincroniza Status, rango, total, paginador y captura masiva; verifica tamaño efectivo y botón siguiente; cierra la página si vence su lectura.
- `PamAutomation.Core/Resilience/PamOperationTimingPolicy.cs`: límites específicos de 30 segundos por lectura y 120 segundos por etapa, además del máximo de cinco segundos por control.
- `PamAutomation.Core/Requests/PamPendingService.cs`: propaga el presupuesto compartido y no reutiliza páginas cerradas por vencimiento.
- `PamAutomation.Core/Diagnostics/PendingReconciliationDiagnostic.cs`: duración por lectura y `PendingBrowserReadDiagnostic` inmutable con operación y conteos.
- `PamAutomation.Tests/Browser/PamPendingGridTransitionTests.cs`: prueba el lector completo mediante páginas locales servidas desde C#, sin red ni credenciales, incluidos cambios tardíos y paginación real.
- `PamAutomation.Tests/Browser/VisiblePlaywrightEngineFactory.cs`: adaptador exclusivo de pruebas que muestra Edge temporal sin cambiar el modo oculto de producción.
- `PamAutomation.Tests/Browser/PamRealPendingTests.cs`: tres lecturas consecutivas opt-in de la sesión autenticada; solo informa conteos y tiempos, sin capturas ni solicitudes nuevas.

No se añaden dependencias NuGet ni proyectos.

## Pending vacío (22 de septiembre de 2026)

La corrección posterior de Pending vacío conserva esta estructura: `PlaywrightManagedPage.Pending.cs` utiliza el resultado estable inicial para omitir el ajuste de tamaño únicamente con total cero; el escaneo posterior revalida la vista. `PamPendingGridTransitionTests.cs` añade casos de tabla vacía con selector deshabilitado, ausente o habilitado, sin paginador, cero temporal durante carga y cero incoherente/desconocido. Son pruebas del lector completo con Edge visible y respuestas locales, sin crear solicitudes. No introduce módulos ni dependencias.

## Observación de Outlook después del envío (22 de septiembre de 2026)

- `PamAutomation.Core/Interfaces/Outlook/IOutlookClassicClient.cs`: separa la consulta previa de duplicados de `ObserveDeliveryAsync`, con ampliación opcional a carpetas no enviadas en la última observación.
- `PamAutomation.Core/Outlook/OutlookDeliveryConfirmationService.cs`: consulta Enviados hasta quince veces, revisa Outbox/Drafts al final, reintenta solo fallos reconocidos y conserva el resultado inicial sin reenviar.
- `PamAutomation.Core/Outlook/OutlookOperationException.cs` y `OutlookOperationErrorCode.cs`: identifican la observación fallida y su recuperabilidad explícita, que por defecto es falsa.
- `PamAutomation.Outlook/OutlookClassicClient.cs`: aplica el alcance, clasifica fallos COM de lectura, mide duración y delega el registro de advertencias/errores posteriores al envío en Core.
- `PamAutomation.Outlook/Interop/OutlookComApplicationFactory.cs`: utiliza `Items.Restrict` por asunto estándar, verifica clase y huella exacta, y preserva fallos de lectura en lugar de devolver ausencia.
- `PamAutomation.Outlook/Interop/OutlookMessageLookupException.cs`: contexto interno seguro de carpeta, operación, conteo e índice del candidato y HRESULT, sin contenido del correo.
- `PamAutomation.Tests/Outlook/OutlookMessageLookupTests.cs`: ejecuta el recorrido tardío del adaptador real sobre objetos simulados públicos; verifica filtros, alcance, propiedades y fallos sin COM real.
- Las pruebas existentes de cliente, confirmación y coordinación verifican además un único envío, recuperación, errores permanentes y ausencia de registros ERROR/WARNING duplicados. Los dobles de configuración y ViewModel implementan el contrato ampliado.

No se agregan proyectos ni dependencias NuGet.

## Copia de Request ID desde resultados (22 de septiembre de 2026)

La ventana `PamAutomation.App/Views/AutomationResultWindow.xaml` y su code-behind presentan los resultados con IDs y gestionan la copia explícita al portapapeles como operación de interfaz. `IUserDialogService.ShowResult` admite un texto opcional de IDs separado del mensaje; `WpfUserDialogService` elige esa ventana modal solo cuando hay IDs y conserva los mensajes existentes para los demás avisos. `MainWindowViewModel.ShowResult` entrega el texto ya formateado en memoria. La prueba existente de presentación valida ese dato separado. No cambia Core, Browser, Outlook ni se añaden dependencias.

## Fase 26 — Correo por colección y firma común (27 de septiembre de 2026)

- `Core/Models/Configuration/CollectionMailSettings.cs`: destinatarios y contenido, sin remitente ni firma; `FavoriteCollection.Mail` conserva una copia independiente y `FavoriteCollectionsState` usa esquema 3.
- `Core/Models/Configuration/MailSignature.cs` e `Interfaces/Configuration/IMailSignatureStore.cs`: firma opcional del principal y contrato de persistencia separado.
- `Core/Outlook/CollectionMailComposer.cs`: resolución del correo activo, saneamiento de firma y composición de plantilla efímera con CID sin colisiones. La sustitución de IDs y las barreras de duplicados existentes siguen en el flujo de Outlook.
- `Infrastructure/Configuration/DpapiFavoriteCollectionStore.cs`: migra esquemas 1/2 con copias del correo legado, guarda atómicamente y vuelve a leer el nuevo estado. `DpapiMailSignatureStore.cs` cifra y reemplaza la firma; ambos usan DPAPI CurrentUser. `DpapiMailSettingsStore` queda para compatibilidad de migración.
- `App/Views/FavoriteCollectionEditorWindow`: guarda un borrador de correo junto con la colección; `FavoritesCatalogWindow` recibe el remitente principal. `MailSettingsWindow` reutiliza el editor enriquecido para contenido por colección y para firma, sin exponer SMTP editable.
- `App/ViewModels/MainWindowViewModel`: firma desde la pantalla principal, resolución del correo al seleccionar colección y antes de confirmar, bloqueo de Outlook incompleto y paso de la misma configuración al flujo normal y a Pending existente. Se retira `Services/MailConfigurationEdit.cs`, que solo representaba la edición global anterior.
- `Tests/Core/Outlook/CollectionMailComposerTests.cs`, `Tests/Infrastructure/Configuration/CollectionMailMigrationTests.cs` y las pruebas ampliadas de colecciones, coordinación y ventanas verifican separación, migración, DPAPI, cancelaciones y ambas rutas de correo con dobles de PAM/Outlook. No se agregaron dependencias ni proyectos.

## Fase 27 — Acumulación en memoria y correo manual

El selector `AccumulationModeToggle` de `MainWindow.xaml` es un `ToggleButton` a la derecha de la acción final; reutiliza `OutlookActionToggleStyle` con contenido propio. No introduce estilos ni módulos adicionales.

Su altura es automática con mínimo de 64 DIP. `Resources/Styles.xaml` define los colores grises del estado deshabilitado del estilo compartido; se conservan las dimensiones horizontales para caber en la ventana mínima.

- `Core/Execution/RequestIdAccumulator.cs`: estado de sesión, GUID vinculado, incorporación atómica/deduplicación y transición Ready/Sending/AwaitingConfirmation; no depende de persistencia ni WPF.
- `Core/Favorites/FavoriteCollectionService.cs`: aplica el bloqueo de selección/desactivación/eliminación y retiene el vínculo al reconciliar Favorites; permite corregir correo de la colección vinculada vacía.
- `Core/Outlook/OutlookDeliveryUncertainException.cs`: distingue fallos que pueden haber producido un mensaje externo y prohíben reenvío ciego. `IPamAutomationExecutionService` y su implementación añaden envío acumulativo protegido y observación sin volver a enviar; reutilizan `OutlookDeliveryConfirmationService` y el cliente Outlook existente.
- `App/ViewModels/MainWindowViewModel.Accumulation.cs`: parcial pequeña para controles, confirmación, instantánea de correo, comprobación de envío, descarte y decisión de cierre. El flujo principal agrega solo resultados completos, suprime la acción final y bloquea reutilización de Pending en este modo.
- `App/MainWindow.xaml`, `MainWindow.xaml.cs` y `App.xaml.cs`: controles y contador, evento de cierre/fin de sesión y una instancia compartida del acumulador. `WpfUserDialogService` presenta las confirmaciones; `WpfConfigurationDialogService`, filas/VM de Favorites y editor de colección propagan la restricción a selección y correo.
- `Tests/Core/Execution/RequestIdAccumulatorTests.cs` y `Tests/MainWindowViewModelAccumulationTests.cs`: integridad, fallos, confirmación, instantánea incierta, concurrencia y cierre. Se amplían pruebas de servicio de colecciones, correo, filas y ventanas WPF. Evidencia local: 119 pruebas relacionadas aprobadas.

Sin módulos nuevos en Infrastructure, Browser u Outlook; sin almacenamiento adicional, proyectos ni dependencias. La reconciliación de Pending y el envío COM conservan su implementación.

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

La corrección del bloqueo de publicación modifica únicamente la comprobación existente `RepeatedPendingDecisionWindow_CompleteSetWithoutEmail_RequiresOutlook` en `PamAutomation.Tests/App/ConfigurationWindowSmokeTests.cs`: valida el bloqueo de reutilización y el aviso vigente. No cambia la estructura, los proyectos, las dependencias ni el script de publicación.

Al terminar cada fase se debe registrar:

- Nuevos proyectos o carpetas creados.
- Responsabilidad de cada módulo nuevo.
- Referencias agregadas o eliminadas entre proyectos.
- Dependencias NuGet relevantes.
- Diferencias entre la estructura planeada y la implementada.
- Elementos objetivo que ya pasaron a estado actual.

## Estabilización de calendario y resultados — fase 28

- Core/Diagnostics/PamExecutionAttempt.cs: contexto asíncrono acotado de un intento real, contador sincronizado, etapa inicial del fallo, fecha normalizada y versiones de navegador; conecta los logs con la exportación sin persistencia ni identidades.
- Core/Requests/PamCalendarDiagnostic.cs y PamCalendarSelectionException.cs: condiciones tipadas e historial de hasta 20 observaciones, anexado al fallo compatible.
- Browser/PamCalendarDateSelector.cs y PlaywrightManagedPage.RequestTest.cs: diagnóstico del calendario, disponibilidad, estabilidad de controles y una corrección limitada. PamRequestFormStabilityTests.cs usa HTML local y Edge externo, con red interceptada en C#.
- `Browser/PamCalendarDateSelector.cs`: durante la lectura inicial, puede reabrir una vez el calendario desaparecido usando el campo de fecha original y el plazo restante. `Core/Requests/PamCalendarDiagnostic.cs` tipifica el intento, el bloqueo, el fallo y la recuperación. `Tests/Browser/PamCalendarDateSelectorTests.cs` reproduce la desaparición con Edge visible y comprueba el límite de un clic, el bloqueo si se reemplaza el campo y la ausencia de reapertura en transiciones.
- Browser/PlaywrightManagedPage.RequestSubmission.cs: frontera irreversible, evidencia de clic/POST/respuesta y conservación del conflicto conocido ante fallo de limpieza. No introduce reenvíos.
- Core/Requests/PamRequestSubmissionService.cs, PamRequestRunResult.cs y PamRequestSubmissionException.cs: instantánea defensiva de todas las filas de un lote interrumpido. Core/Execution/PamAutomationExecutionService.cs reutiliza Pending para recuperar grupos completos; PamPendingService.cs permite observar comparaciones parciales sin cambiar sus reglas.
- Infrastructure/Diagnostics: logger filtra metadata tipada, store recibe conteos sin IProgress y exporter separa último intento/éxito, identifica MVID/versiones de ensamblados conocidos. App/App.xaml.cs comparte el store; MainWindowViewModel muestra resultados incompletos sin incorporarlos al acumulado.
- Tests: amplían calendario, servicios, ejecución/Pending, logger/ZIP y acumulación; matriz real de formularios descartados para noviembre/diciembre de 2026 y enero/febrero de 2027. Los reportes de desarrollo se guardan en results ignorado; no se generan archivos operativos de producción.

No hay proyectos, dependencias NuGet ni versiones de aplicación nuevas. RequestIdAccumulator y la navegación Favorites permanecen con sus contratos. El margen temporal está diferido.
