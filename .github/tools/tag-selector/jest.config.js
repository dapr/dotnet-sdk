module.exports = {
    testEnvironment: 'node',
    transform: {
        '^.+\\.tsx?$': ['ts-jest', { tsconfig: './tsconfig.json', isolatedModules: true }],
    },
    roots: ['<rootDir>/src', '<rootDir>/test'],
    moduleFileExtensions: ['ts', 'js', 'json'],
    collectCoverageFrom: ['src/**/*.ts', '!src/index.ts'], // index.ts is just wiring
    coverageReporters: ['text', 'lcov', 'cobertura'],
};