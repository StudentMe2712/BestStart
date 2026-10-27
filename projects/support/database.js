const mysql = require('mysql2/promise');

// Подключение к MariaDB (используем тот же пул, что и для auth)
const pool = mysql.createPool({
    host: process.env.DB_HOST || 'localhost',
    port: process.env.DB_PORT || 3306,
    user: process.env.DB_USER || 'p-14802_adminits',
    password: process.env.DB_PASSWORD || 'ASDFzxcv@113525',
    database: process.env.DB_NAME || 'p-14802_wiki',
    charset: 'utf8mb4',
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
                content LONGTEXT DEFAULT '',
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
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

// ============================================================
// Инициализация таблицы закреплений организаций (user_pins - Спринт 7)
// ============================================================
async function initUserPinsTable() {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(`
            CREATE TABLE IF NOT EXISTS user_pins (
                user_id INT NOT NULL,
                org_id INT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (user_id, org_id)
            )
        `);
        console.log('✓ Таблица user_pins готова');
    } catch (err) {
        console.error('Ошибка инициализации таблицы user_pins:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Функции для работы с закреплениями пользователей
const userPinsDB = {
    // Получить массив ID закрепленных организаций пользователя
    getUserPins: async (userId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query('SELECT org_id FROM user_pins WHERE user_id = ? ORDER BY created_at ASC', [Number(userId)]);
            return rows.map(r => Number(r.org_id));
        } catch (err) {
            console.error(`Ошибка получения закреплений пользователя ${userId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Переключить закрепление организации (если закреплено - удалить, иначе добавить)
    toggleUserPin: async (userId, orgId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const uId = Number(userId);
            const oId = Number(orgId);
            const [existing] = await connection.query('SELECT user_id FROM user_pins WHERE user_id = ? AND org_id = ?', [uId, oId]);
            let pinned = false;
            if (existing.length > 0) {
                await connection.query('DELETE FROM user_pins WHERE user_id = ? AND org_id = ?', [uId, oId]);
                pinned = false;
            } else {
                await connection.query('INSERT INTO user_pins (user_id, org_id) VALUES (?, ?)', [uId, oId]);
                pinned = true;
            }
            const [rows] = await connection.query('SELECT org_id FROM user_pins WHERE user_id = ? ORDER BY created_at ASC', [uId]);
            return { pinned, pins: rows.map(r => Number(r.org_id)) };
        } catch (err) {
            console.error(`Ошибка переключения закрепления user ${userId}, org ${orgId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    }
};

// ============================================================
// Инициализация таблицы прикрепленных файлов (organization_files - Спринт 7)
// ============================================================
async function initFilesTable() {
    let connection;
    try {
        connection = await pool.getConnection();
        await connection.query(`
            CREATE TABLE IF NOT EXISTS organization_files (
                id INT AUTO_INCREMENT PRIMARY KEY,
                org_id INT NOT NULL,
                filename VARCHAR(255) NOT NULL,
                original_name VARCHAR(255) NOT NULL,
                mime_type VARCHAR(100) NOT NULL,
                file_size INT NOT NULL,
                uploaded_by VARCHAR(50) DEFAULT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                INDEX (org_id),
                FOREIGN KEY (org_id) REFERENCES organizations(id) ON DELETE CASCADE
            ) DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
        `);
        console.log('✓ Таблица organization_files готова');
    } catch (err) {
        console.error('Ошибка инициализации таблицы organization_files:', err.message);
        throw err;
    } finally {
        if (connection) connection.release();
    }
}

// Функции для работы с файлами организаций
const filesDB = {
    // Получить список файлов организации (сортировка по новизне DESC)
    getByOrgId: async (orgId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query('SELECT * FROM organization_files WHERE org_id = ? ORDER BY created_at DESC', [Number(orgId)]);
            return rows;
        } catch (err) {
            console.error(`Ошибка получения файлов для организации ${orgId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Получить метаданные файла по ID
    getById: async (fileId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [rows] = await connection.query('SELECT * FROM organization_files WHERE id = ?', [Number(fileId)]);
            return rows[0] || null;
        } catch (err) {
            console.error(`Ошибка получения файла по ID ${fileId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Добавить запись о новом файле
    create: async ({ org_id, filename, original_name, mime_type, file_size, uploaded_by }) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query(
                'INSERT INTO organization_files (org_id, filename, original_name, mime_type, file_size, uploaded_by) VALUES (?, ?, ?, ?, ?, ?)',
                [Number(org_id), filename, original_name, mime_type, Number(file_size), uploaded_by || null]
            );
            const [rows] = await connection.query('SELECT * FROM organization_files WHERE id = ?', [result.insertId]);
            return rows[0];
        } catch (err) {
            console.error('Ошибка добавления файла в БД:', err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Удалить запись о файле по ID
    delete: async (fileId) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query('DELETE FROM organization_files WHERE id = ?', [Number(fileId)]);
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка удаления файла по ID ${fileId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    },

    // Обновить размер файла (Спринт 11)
    updateFileSize: async (fileId, newSize) => {
        let connection;
        try {
            connection = await pool.getConnection();
            const [result] = await connection.query(
                'UPDATE organization_files SET file_size = ? WHERE id = ?',
                [Number(newSize), Number(fileId)]
            );
            return result.affectedRows;
        } catch (err) {
            console.error(`Ошибка обновления размера файла по ID ${fileId}:`, err.message);
            throw err;
        } finally {
            if (connection) connection.release();
        }
    }
};

// Алиасы методов filesDB для обратной совместимости
filesDB.getFilesByOrg = filesDB.getByOrgId;
filesDB.getFileById = filesDB.getById;
filesDB.addFile = filesDB.create;
filesDB.deleteFile = filesDB.delete;
filesDB.updateFileSize = filesDB.updateFileSize;

module.exports = {
    initOrganizationTable,
    organizationsDB,
    initDatabases1CTable,
    databases1CDB,
    initUserPinsTable,
    userPinsDB,
    initFilesTable,
    filesDB
};


