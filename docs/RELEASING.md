# Publicar una versión

## 1. Versión

1. Sube `<Version>` en `Parpa.App/Parpa.App.csproj` (la ven Ajustes y el menú de bandeja).
2. Commit en `main` y etiqueta:

   ```bash
   git tag v1.0.0
   ```

   ```bash
   git push origin main --tags
   ```

   Repositorio: <https://github.com/Xzorez/Parpa>.

El flujo **Publicar versión** (`.github/workflows/release.yml`) crea, para x64 y arm64:

- `Parpa-Setup-<versión>-<arch>.exe` (Inno Setup, por usuario, sin administrador),
- `SHA256SUMS.txt`,
- `winget-manifests-<versión>.zip`,

y deja un **borrador** en GitHub Releases. Revísalo, escribe las novedades y pulsa *Publish release*.

Para probar el instalador en local: `pwsh tools/build-installer.ps1 -Version 1.0.0` (necesita Inno Setup 6: `winget install JRSoftware.InnoSetup`).

## 2. Firma con SignPath (gratis para código abierto)

Sin firma, Windows SmartScreen avisa de "editor desconocido" al instalar. SignPath Foundation firma gratis proyectos open source con su certificado.

1. Solicita el proyecto en <https://signpath.org/apply> (repositorio público, licencia MIT, compilación en GitHub Actions: ya cumple las tres).
2. En SignPath, crea el proyecto con dos **configuraciones de artefacto**:

   **`app`** — solo los binarios propios de Parpa, antes de empaquetar:

   ```xml
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <pe-file path="Parpa.exe"><authenticode-sign /></pe-file>
       <pe-file path="Parpa.dll"><authenticode-sign /></pe-file>
     </zip-file>
   </artifact-configuration>
   ```

   **`installer`** — el instalador:

   ```xml
   <artifact-configuration xmlns="http://signpath.io/artifact-configuration/v1">
     <zip-file>
       <pe-file-set>
         <include path="Parpa-Setup-*.exe" />
         <for-each><authenticode-sign /></for-each>
       </pe-file-set>
     </zip-file>
   </artifact-configuration>
   ```

3. Conecta el repositorio de GitHub como *Trusted Build System* en SignPath.
4. En GitHub › Settings › Secrets and variables › Actions:
   - Secreto `SIGNPATH_API_TOKEN`.
   - Variables `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG` y `SIGNPATH_SIGNING_POLICY_SLUG` (p. ej. `release-signing`).

Con `SIGNPATH_ORGANIZATION_ID` definida, el flujo firma automáticamente; sin ella, publica sin firmar.

## 3. winget

La primera vez (y en cada versión, hasta que se automatice):

1. Descarga `winget-manifests-<versión>.zip` del release publicado.
2. Haz fork de <https://github.com/microsoft/winget-pkgs> y copia los cuatro `.yaml` en `manifests/p/Parpa/Parpa/<versión>/`.
3. Compruébalo:

   ```bash
   winget validate --manifest manifests/p/Parpa/Parpa/1.0.0
   ```

4. Abre el pull request. Cuando lo aceptan, funciona `winget install Parpa`.

Los manifiestos apuntan a las URL del release, así que el release tiene que estar **publicado** (no en borrador) antes de abrir el pull request.
