const mysql = require('mysql2/promise');

(async () => {
    try {
        const pool = mysql.createPool({
            host: 'localhost',
            user: 'p-14802_adminits',
            password: 'ASDFzxcv@113525',
            database: 'p-14802_wiki',
            waitForConnections: true,
            connectionLimit: 5
        });

        console.log('Попытка соединения с базой...');
        const conn = await pool.getConnection();
        console.log('Соединение успешно!');
        await conn.query('SELECT NOW() AS time');
        console.log('Запрос к базе прошел успешно!');
        conn.release();
        await pool.end();
    } catch (err) {
        console.error('Ошибка соединения с БД:', err);
    }
})();
