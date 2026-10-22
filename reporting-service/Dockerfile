FROM --platform=$BUILDPLATFORM golang:1.26-alpine AS build
ARG TARGETOS
ARG TARGETARCH
WORKDIR /src
COPY go.mod ./
COPY *.go ./
RUN CGO_ENABLED=0 GOOS=$TARGETOS GOARCH=$TARGETARCH go build -trimpath -ldflags="-s -w" -o /reporting-service .

FROM alpine:3.23
RUN apk add --no-cache ca-certificates && mkdir /data && chmod 700 /data
COPY --from=build /reporting-service /usr/local/bin/reporting-service
ENV DATA_DIR=/data
EXPOSE 8080
ENTRYPOINT ["/usr/local/bin/reporting-service"]
