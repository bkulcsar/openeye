import { createClient } from "redis";

const globalForRedis = globalThis as unknown as { redis: ReturnType<typeof createClient> };

const redisUrl = process.env.REDIS_URL ?? "redis://localhost:6379";

export const redis = globalForRedis.redis ?? createClient({ url: redisUrl });

if (!globalForRedis.redis) {
  redis.connect().catch(console.error);
  globalForRedis.redis = redis;
}

export async function publishConfigChanged(section: string) {
  try {
    await redis.publish("config:changed", section);
  } catch (err) {
    console.error("Failed to publish config:changed", err);
  }
}
