FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 80

COPY ./restic-manager-release /app/

ENTRYPOINT ["dotnet", "ResticManager.Web.dll"]