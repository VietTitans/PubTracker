# Build stage
FROM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .

# Vite inlines VITE_* env vars into the bundle at build time, so they must be passed as
# build args (not container runtime env) - the values need to be reachable from the
# user's browser, not from inside the Docker network.
ARG VITE_API_BASE_URL
ARG VITE_KEYCLOAK_AUTHORITY
ARG VITE_KEYCLOAK_CLIENT_ID
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_KEYCLOAK_AUTHORITY=$VITE_KEYCLOAK_AUTHORITY \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID

RUN npm run build

# Serve stage
FROM nginx:1.27-alpine

COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
