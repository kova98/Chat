// Long Polling load test

import http from 'k6/http';
import { sleep } from 'k6';

export const options = {
    vus: 30,
    duration: '30s',
};

export function setup () {
    
}

export default function () {
    // Initial connection
    const rnd = Math.floor(Math.random() * 1000000);
    const user = `user${rnd}`;
    let lpUrl = `http://localhost:5000/lp?name=${user}`;
    const params = {
        headers: {
            'Content-Type': 'application/json',
        },
    };

    const response = http.get(lpUrl, params);
    const id = response.headers['X-Connection-Id']
       
    while (true) {
        // Send a message
        const rnd = Math.floor(Math.random() * 1000000);
        const msg = `msg${rnd}`;
        const data = JSON.stringify({ 
            "Name": user,
            "Content": msg,
        });
        
        const msgUrl = `http://localhost:5000/lp/message`;
        http.post(msgUrl, data, params);
        
        // Wait for messages
        lpUrl = `http://localhost:5000/lp?name=${user}&id=${id}`;
        const response = http.get(lpUrl, params);
    }
    
    sleep(1);
}
