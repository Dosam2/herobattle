# ─── Build Stage ───
FROM node:22-alpine AS builder

RUN apk add --no-cache git

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY tsconfig.json ./
COPY src/ ./src/

RUN npm run build

# ─── Production Stage ───
FROM node:22-alpine

RUN apk add --no-cache git

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install --production

COPY --from=builder /app/dist ./dist

EXPOSE 7777
EXPOSE 8888

ENV GAME_PORT=7777
ENV MONITOR_PORT=8888
ENV NODE_ENV=production

CMD ["node", "dist/main.js"]
