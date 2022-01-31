# BTCexchange project 

A simple BTC exchange REST API with Swagger. Allows for registering user, posting orders to sell/buy BTC and pairs these orders and executes them. 

Assignment from https://docs.google.com/document/d/1V2myhqjxi_seDJlUChEq9saD0BtnAVX3dGQuE6csbxQ/edit
Made in Visual studio, using framework ASP.NET core.
Endpoints are on https://localhost:7290/swagger/index.html available also through Swagger interface. 

Uses a PostgreSQL database and database-first approach, so you need to have PostgreSQL installed and created tables (created directly through pgAdmin):

create table users (
id serial primary key,
name text NOT NULL,
token text NOT NULL,
btc_balance int8 NOT NULL,
usd_balance int8 NOT NULL
);

create table orders (
id serial primary key, 
user_id int4 NOT NULL,
filled_quantity int8 NOT NULL,
remain_quantity int8 NOT NULL,
limit_price int8 NOT NULL,	
avg_price double precision NOT NULL,
status text NOT NULL,
buying bool NOT NULL,
notify_url text	
);
