# Terrace.Server を Linux のコンテナで動かす像。Windows の Smart App Control がサーバーの DLL を止める PC でも動く
# (理由は docs/decisions/0011-server-in-docker.md)。普段は tools/server-docker.ps1 から使う。
#
# 文脈はリポジトリのルート(兄弟の Terrace.Map / Terrace.MasterData を参照するため)。文脈に入れるものはルートの .dockerignore。
#   docker build -f Terrace.Server/Dockerfile -t terrace-server .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Terrace.Map/ Terrace.Map/
COPY Terrace.MasterData/ Terrace.MasterData/
COPY Terrace.Server/ Terrace.Server/
# NuGet のキャッシュは像の外(BuildKit のキャッシュ)に持ち、作り直すたびに取り直さない
RUN --mount=type=cache,id=terrace-nuget,target=/root/.nuget/packages \
    dotnet publish Terrace.Server/src/Terrace.Server -c Release -o /app --nologo

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.title=terrace-server
WORKDIR /app
COPY --from=build /app .
# コンテナの中では全インターフェースで待ち受ける。外へどう開けるかは docker run -p で決める
# (server-docker.ps1 は既定でこの PC の中からだけ繋げるように開ける)。
# ASPNETCORE_HTTP_PORTS は像の既定(8080)を消す。待ち受けは Program.cs の Kestrel の設定だけにする
ENV Terrace__ListenAnyIP=true \
    Terrace__GrpcPort=5000 \
    Terrace__HttpPort=5001 \
    ASPNETCORE_HTTP_PORTS=
EXPOSE 5000 5001
USER $APP_UID
ENTRYPOINT ["dotnet", "Terrace.Server.dll"]
