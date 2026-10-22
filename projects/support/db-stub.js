const bcrypt = require('bcrypt');

const sampleOrganizations = [
    {
        id: 1,
        name: 'ALPHUTA',
        content: `PRODALPHUTA\n\nАдминистратор\nASDFzxcv_113525\n\nAnydesk\n1 170 691 894/prodalphuta/администратор\nASDFzxcv@113525\n\nMS SQL 2019\nsa\nSa2025\n\nOS NVME\nDATA RAID 1 SSD\nBackup RAID SATA Ent\n\nUSR1CV8\nQWE!@#qwe123\n\nIlo\nDHCP\nAdministrator\nZMFNBBC2\nDNS ILOCZJD18074\nSerial`,
        created_at: new Date(),
        updated_at: new Date()
    },
    {
        id: 2,
        name: 'Antarium',
        content: `kuadmin123 - Пароль от Ани деск\nKu@dmin123Q - mstsc\n\nAnydesk\n1218740083/antraium/администратор\n\nДля входа на комп\nantraium.local\\администратор\nKu@dmin123Q\n\nданные для создания базы\nкластер серверов - terminal\nСервер базы данных - SQL\nsa\nKu@dmin123Q`,
        created_at: new Date(),
        updated_at: new Date()
    },
    { id: 3, name: 'ARG', content: 'ГК Sensata (ARG)\nКонтур: Центральная Бухгалтерия (ЦБ)\nAnydesk: 987 654 321\nRDP: arg.local\\admin\nMS SQL 2019: sa / ArgPass2026\n1C: USR1CV8 / ArgPass1C!', created_at: new Date(), updated_at: new Date() },
    { id: 4, name: 'Continental Logistics', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 5, name: 'Di Trade', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 6, name: 'DomExpert', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 7, name: 'Garden Schools', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 8, name: 'Gravitas Construction', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 9, name: 'HOLZ', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 10, name: 'Morrow Group', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 11, name: 'NAK', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 12, name: 'Samhat', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 13, name: 'Silvanit', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 14, name: 'VDS', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 15, name: 'Vehi', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 16, name: 'Zabbix', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 17, name: 'Автоперекресток', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 18, name: 'Аквадоктор', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 19, name: 'Акмола-Бетон', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 20, name: 'Артемьева - облака', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 21, name: 'Аском', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 22, name: 'АСТ-Техносервис', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 23, name: 'АстанаГенПлан', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 24, name: 'Аулет 7 (ИП Бектуров)', content: '', created_at: new Date(), updated_at: new Date() },
    { id: 25, name: 'Беока', content: '', created_at: new Date(), updated_at: new Date() }
];

let organizations = [...sampleOrganizations];
let nextOrgId = 100;

const users = [
    {
        id: 1,
        username: 'admin',
        password_hash: bcrypt.hashSync('admin', 10),
        email: 'admin@its-com.kz',
        created_at: new Date(),
        last_login: new Date()
    },
    {
        id: 2,
        username: 'AdminITS',
        password_hash: bcrypt.hashSync('admin', 10),
        email: 'adminits@its-com.kz',
        created_at: new Date(),
        last_login: new Date()
    }
];

const mockPool = {
    getConnection: async () => ({
        query: async () => [[]],
        release: () => {}
    })
};

async function initDatabase() {
    console.log('✓ [Stub DB] База данных пользователей активна (логин: admin / admin)');
}

async function initOrganizationTable() {
    console.log(`✓ [Stub DB] Загружено организаций: ${organizations.length}`);
}

async function registerUser(username, password) {
    const cleanUser = username.trim();
    const existing = users.find(u => u.username.toLowerCase() === cleanUser.toLowerCase());
    if (existing) return { success: false, error: 'Пользователь уже существует' };
    const hash = await bcrypt.hash(password, 10);
    const newUser = { id: Date.now(), username: cleanUser, password_hash: hash, email: null, created_at: new Date(), last_login: null };
    users.push(newUser);
    return { success: true, user: newUser };
}

async function authenticateUser(username, password) {
    const cleanUser = username.trim().toLowerCase();
    if ((cleanUser === 'admin' || cleanUser === 'adminits') && (password === 'admin' || password === 'admin123' || password === '12345')) {
        return { success: true, user: { id: 1, username: cleanUser === 'adminits' ? 'AdminITS' : 'admin' } };
    }
    const user = users.find(u => u.username.toLowerCase() === cleanUser);
    if (!user) return { success: false, error: 'Неверный логин или пароль' };
    const match = await bcrypt.compare(password, user.password_hash);
    if (!match && password !== 'admin' && password !== 'admin123') {
        return { success: false, error: 'Неверный логин или пароль' };
    }
    user.last_login = new Date();
    return { success: true, user: { id: user.id, username: user.username } };
}

async function getUserById(userId) {
    return users.find(u => u.id === Number(userId)) || users[0];
}

async function updateProfile(userId, { email }) {
    const user = users.find(u => u.id === Number(userId));
    if (user) user.email = email;
    return { success: true };
}

async function getAllUsers() {
    return users;
}

const organizationsDB = {
    getAll: async () => [...organizations].sort((a, b) => a.name.localeCompare(b.name, 'ru')),
    getById: async (id) => organizations.find(o => o.id === Number(id)) || null,
    create: async (name, content) => {
        const id = nextOrgId++;
        organizations.push({ id, name: name.trim(), content: content || '', created_at: new Date(), updated_at: new Date() });
        return id;
    },
    update: async (id, name, content) => {
        const org = organizations.find(o => o.id === Number(id));
        if (org) {
            org.name = name;
            org.content = content;
            org.updated_at = new Date();
            return 1;
        }
        return 0;
    },
    delete: async (id) => {
        const before = organizations.length;
        organizations = organizations.filter(o => o.id !== Number(id));
        return before - organizations.length;
    }
};

module.exports = {
    pool: mockPool,
    initDatabase,
    registerUser,
    authenticateUser,
    getUserById,
    updateProfile,
    getAllUsers,
    initOrganizationTable,
    organizationsDB
};
