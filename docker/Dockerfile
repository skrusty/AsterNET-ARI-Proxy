FROM mono:latest

RUN apt-get update
RUN apt-get install -y git
RUN mkdir /usr/src/asternet-ari-proxy

WORKDIR /usr/src/asternet-ari-proxy
RUN git clone https://github.com/skrusty/AsterNET-ARI-Proxy.git

WORKDIR /usr/src/asternet-ari-proxy/AsterNET-ARI-Proxy
RUN nuget restore
RUN xbuild /p:Configuration=Release asternet-proxy/AsterNET.ARI.Proxy.csproj

RUN mkdir /usr/local/asternet-ari-proxy
RUN cp -R asternet-proxy/bin/Release/* /usr/local/asternet-ari-proxy

WORKDIR /usr/local/asternet-ari-proxy
ENTRYPOINT mono asternet-ari-proxy.exe
