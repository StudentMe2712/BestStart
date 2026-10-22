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

// Инициализация таблицы баз 1С
async function initDatabases1CTable() {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(`
            CREATE TABLE IF NOT EXISTS organization_databases_1c (
                id INT AUTO_INCREMENT PRIMARY KEY,
                org_id INT NOT NULL,
                name VARCHAR(255) NOT NULL,
                release_version TEXT NOT NULL,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (org_id) REFERENCES organizations(id) ON DELETE CASCADE
            )
        `);
        console.log('✓ Таблица organization_databases_1c готова');
    } catch (err) {
        console.error('Ошибка инициализации таблицы баз 1С:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Функции для работы с базами 1С
const databases1CDB = {
    // Получить все базы 1С
    getAll: async () => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query(
                'SELECT id, org_id, name, release_version AS `release`, release_version AS `description`, updated_at FROM organization_databases_1c ORDER BY updated_at DESC'
            );
            return rows;
        } catch (err) {
            console.error('Ошибка получения всех баз 1С:', err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Получить все базы 1С организации
    getByOrgId: async (orgId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query(
                'SELECT id, org_id, name, release_version AS `release`, release_version AS `description`, updated_at FROM organization_databases_1c WHERE org_id = ? ORDER BY updated_at DESC',
                [orgId]
            );
            return rows;
        } catch (err) {
            console.error(`Ошибка получения баз 1С для организации ${orgId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Получить все последние обновления для журнала Событий
    getAllRecentUpdates: async () => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query(`
                SELECT d.id, d.org_id, o.name AS org_name, d.name, d.release_version AS \`release\`, d.release_version AS \`description\`, d.updated_at
                FROM organization_databases_1c d
                JOIN organizations o ON d.org_id = o.id
                ORDER BY d.updated_at DESC
            `);
            return rows;
        } catch (err) {
            console.error('Ошибка получения журнала обновлений 1С:', err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Добавить базу 1С
    create: async ({ org_id, name, release, description, updated_at }) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const dateVal = updated_at ? new Date(updated_at) : new Date();
            const descText = (description !== undefined ? description : (release || '')).trim();
            const [result] = await connection.query(
                'INSERT INTO organization_databases_1c (org_id, name, release_version, updated_at) VALUES (?, ?, ?, ?)',
                [org_id, name ? name.trim() : '', descText, dateVal]
            );
            return {
                id: result.insertId,
                org_id: Number(org_id),
                name: name ? name.trim() : '',
                release: descText,
                description: descText,
                updated_at: dateVal
            };
        } catch (err) {
            console.error(`Ошибка создания базы 1С ${name}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Обновить базу 1С
    update: async (id, { name, release, description, updated_at }) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const dateVal = updated_at ? new Date(updated_at) : new Date();
            const descText = description !== undefined ? description : release;
            const cleanDesc = descText !== undefined ? descText.trim() : '';
            const [result] = await connection.query(
                'UPDATE organization_databases_1c SET name = ?, release_version = ?, updated_at = ? WHERE id = ?',
                [name ? name.trim() : '', cleanDesc, dateVal, id]
            );
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка обновления базы 1С ${id}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Удалить базу 1С
    delete: async (id) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query('DELETE FROM organization_databases_1c WHERE id = ?', [id]);
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка удаления базы 1С ${id}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    }
};

module.exports = {
    initOrganizationTable,
    organizationsDB,
    initDatabases1CTable,
    databases1CDB
};


