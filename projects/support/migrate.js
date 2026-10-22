// migrate.js
const { exec } = require('child_process');

exec('npx sequelize db:migrate', (err, stdout, stderr) => {
  if (err) {
    console.error(`Ошибка миграции: ${err.message}`);
    return;
  }
  if (stderr) {
    console.error(`stderr: ${stderr}`);
    return;
  }
  console.log(`stdout: ${stdout}`);
});