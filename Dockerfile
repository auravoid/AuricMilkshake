FROM mcr.microsoft.com/dotnet/sdk:6.0.201-alpine3.14 AS build-env
WORKDIR /app
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet build -c Release -o out
FROM mcr.microsoft.com/dotnet/runtime:6.0.3-alpine3.14
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "MechanicalMilkshake.dll"]
