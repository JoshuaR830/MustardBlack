FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build-env
WORKDIR /app

COPY /MustardBlack/src/MustardBlack.Example.AspNetCore/*.csproj ./
RUN dotnet restore

Copy . ./
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/aspnet:2.2
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "MustardBlack.Example.AspNetCore.dll"]

