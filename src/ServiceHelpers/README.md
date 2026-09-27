# ServiceHelpers/ — Windows service helpers

Two tiny exe projects used by the Windows installer to install / uninstall
Readarr as a Windows service. They are NOT part of the running app — they
exist solely so the installer can invoke them.

## Projects

- `ServiceInstall/` (`ServiceInstall.csproj`) — registers the service with
  the SCM (Service Control Manager).
- `ServiceUninstall/` (`ServiceUninstall.csproj`) — unregisters it.

## Output

Both project names are explicitly mapped to `Exe` in
`../Directory.Build.props:18-19`. Output goes to the install folder so the
InnoSetup installer can shell out to them.

## Why standalone exes?

Service registration requires admin rights. Bundling these as standalone
exes lets the installer prompt for UAC and run them without elevating the
entire main process.
