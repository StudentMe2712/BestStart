const mysql = require('mysql2/promise');

// Подключение к MariaDB (используем тот же пул, что и для auth)
const pool = mysql.createPool({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 3306,
    user: process.env.DB_USER || 'p-14802_adminits',
    password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
    database: process.env.DB_NAME || 'p-14802_wiki',
    waitForConnections: true,
    connectionLimit: 10,
    queueLimit: 0
});

// Проверка подключения
pool.getConnection()
    .then(connection => {
        console.log('✓ Подключено к MariaDB для организаций');
        connection.release();
    })
    .catch(err => {
        console.error('Ошибка подключения к MariaDB для организаций:', err.message);
        process.exit(1);
    });

// Инициализация таблицы организаций
async function initOrganizationTable() {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(`
            CREATE TABLE IF NOT EXISTS organizations (
                id INT AUTO_INCREMENT PRIMARY KEY,
                name VARCHAR(255) NOT NULL UNIQUE,
                content TEXT DEFAULT '',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )
        `);
        console.log('✓ Таблица organizations готова');
    } catch (err) {
        console.error('Ошибка инициализации таблицы организаций:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Функции для работы с организациями
const organizationsDB = {
    // Получить все организации
    getAll: async () => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query('SELECT * FROM organizations ORDER BY name ASC');
            return rows;
        } catch (err) {
            console.error('Ошибка получения всех организаций:', err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Получить организацию по ID
    getById: async (id) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query('SELECT * FROM organizations WHERE id = ?', [id]);
            return rows[0];
        } catch (err) {
            console.error(`Ошибка получения организации по ID ${id}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Создать новую организацию
    create: async (name, content) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query(
                'INSERT INTO organizations (name, content) VALUES (?, ?)',
                [name, content || '']
            );
            return result.insertId;
        } catch (err) {
            console.error(`Ошибка создания организации ${name}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Обновить организацию
    update: async (id, name, content) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query(
                'UPDATE organizations SET name = ?, content = ?, updated_at = CURRENT_TIMESTAMP WHERE id = ?',
                [name, content, id]
            );
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка обновления организации по ID ${id}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Удалить организацию
    delete: async (id) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query('DELETE FROM organizations WHERE id = ?', [id]);
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка удаления организации по ID ${id}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    }
};

module.exports = {
    initOrganizationTable,
    organizationsDB
};


