/// <reference path="../pb_data/types.d.ts" />

// Prepara a collection `users` para o template funcionar de ponta a ponta.
//
// 1) `avatar`       — campo file (foto de perfil, até 5 MB, só imagens).
// 2) `emailVisibility` — campo bool (o e-mail aparece publicamente ou não).
//
// Idempotente: pode rodar várias vezes sem quebrar.
// O `down` nunca derruba nada que possa existir em produção.

migrate((app) => {
  const users = app.findCollectionByNameOrId("users")

  if (!users.fields.getByName("avatar")) {
    users.fields.add(
      new Field({
        name: "avatar",
        type: "file",
        required: false,
        maxSelect: 1,
        maxSize: 5242880, // 5 MB
        mimeTypes: ["image/png", "image/jpeg", "image/gif", "image/webp"],
      })
    )
  }

  if (!users.fields.getByName("emailVisibility")) {
    users.fields.add(
      new Field({
        name: "emailVisibility",
        type: "bool",
        required: false,
      })
    )
  }

  app.save(users)
}, (app) => {
  try {
    const users = app.findCollectionByNameOrId("users")

    for (const field of ["avatar", "emailVisibility"]) {
      if (users.fields.getByName(field)) {
        users.fields.removeByName(field)
      }
    }

    app.save(users)
  } catch {
    // best effort
  }
})
