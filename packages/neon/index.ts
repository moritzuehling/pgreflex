import { createApiClient } from "@neondatabase/api-client";

const apiKey = env("NEON_API_KEY");
const projectId = env("NEON_PROJECT_ID");
const role = env("DB_ROLE_NAME");
const dbName = env("DB_DATABASE_NAME");
const includeArchived = Boolean(process.env["INCLUDE_ARCHIVED"]);

const neon = createApiClient({
  apiKey,
});

const branches = await neon.listProjectBranches({
  projectId,
  limit: 100,
  sort_order: "desc",
  sort_by: "updated_at",
});

const activeBranches = branches.data.branches.filter(
  (branch) =>
    branch.current_state == "ready" ||
    (includeArchived && branch.current_state == "archived"),
);

const urls = await Promise.all(
  activeBranches.map((a) =>
    neon.getConnectionUri({
      projectId,
      role_name: role,
      database_name: dbName,
      branch_id: a.id,
    }),
  ),
);

Bun.file(process.argv[2]!).write(
  JSON.stringify(urls.map((a) => toDotnet(a.data.uri))),
);

function toDotnet(url: string) {
  const p = URL.parse(url);
  return `Host=${p?.hostname};Port=${p?.port ?? "5432"};Username=${p?.username};Password=${encodeURIComponent(p?.password ?? "")};Database=${p?.pathname.substring(1)};SSLMode=${p?.searchParams.get("sslmode") ?? "require"}`;
}

function env(varName: string): string {
  const res = process.env[varName];
  if (!res) {
    console.error("must set", varName);
    process.exit();
  }

  return res;
}
