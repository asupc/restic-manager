FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

COPY ./restic-manager-release /app/
RUN if [ -f /app/restic/0.18.1/restic ]; then chmod 755 /app/restic/0.18.1/restic; fi \
    && mkdir -p /app/data \
    && chown -R $APP_UID:$APP_UID /app/data

USER $APP_UID
ENTRYPOINT ["dotnet", "ResticManager.Web.dll"]