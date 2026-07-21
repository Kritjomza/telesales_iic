FROM node:20-alpine AS build
WORKDIR /src/Frontend
COPY Frontend/package.json Frontend/package-lock.json ./
RUN npm ci
COPY Frontend/ ./
RUN npm run build

FROM nginx:1.26-alpine AS runtime
COPY myserver/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/Frontend/dist/ /usr/share/nginx/html/
EXPOSE 80
