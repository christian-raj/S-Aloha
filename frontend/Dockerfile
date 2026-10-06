FROM docker.io/library/node:24-alpine AS build
WORKDIR /app
COPY package.json .
RUN npm install
COPY . .
# URL du code source affichée dans l'interface (AGPLv3, section 13) — à surcharger par un fork
ARG VITE_SOURCE_URL
ENV VITE_SOURCE_URL=$VITE_SOURCE_URL
RUN npm run build

FROM docker.io/library/nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
