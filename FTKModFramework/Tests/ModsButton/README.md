# Mods title button lifecycle regression

Run `dotnet run --project FTKModFramework/Tests/ModsButton -c Release`.

The executable links the production patch against small Unity UI stand-ins. It
checks injection on two distinct title screens, repeated focus without duplicate
cells, preservation of the earlier screen, and retry after a missing parent.
This covers the process-static suppression regression when native gameplay exit
recreates the title screen. It does not prove native layout, navigation, event
wiring, or panel opening. Verify those by entering a run and exiting through the
native menu, then opening Mods on the newly created title screen.
