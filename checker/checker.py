#!/usr/bin/env python3
import functools
import json
import logging
import random
import string
import time
from typing import List

import dicttoxml
import requests
import xmltodict
from bs4 import BeautifulSoup
from ctf_gameserver import checkerlib
from ctf_gameserver.checkerlib import CheckResult

dicttoxml.LOG.setLevel(logging.ERROR)
logger = logging.getLogger()
string_length = 8
todos_to_check = 3
TIMEOUT = None


def get_random_email():
    return generate_random_string(random.randint(7, 10)) + "@" + generate_random_string(random.randint(6, 8)) + ".com"


def password_requirement(passwd):
    if not any(char.isdigit() for char in passwd):
        return False
    if not any(char.isupper() for char in passwd):
        return False
    if not any(char.islower() for char in passwd):
        return False
    return True


def generate_random_password(length: int, seed: int, special_char=False) -> str:
    myrandom = random.Random()
    if seed != -1:
        myrandom = random.Random(seed)
    while True:
        characters = string.ascii_letters + string.digits
        if special_char:
            characters += string.punctuation
        random_string = ''.join(myrandom.choices(characters, k=length))
        random_string += "!"
        if password_requirement(random_string):
            return random_string


def generate_random_string(length: int, special_char=False) -> str:
    characters = string.ascii_letters + string.digits
    if special_char:
        characters += string.punctuation
    return ''.join(random.choices(characters, k=length))


def extract_verification_token(html_content: str):
    soup = BeautifulSoup(html_content, "html.parser")
    return soup.find("input", {"name": "__RequestVerificationToken"})["value"]


def extract_todos(html_content: str) -> List[str]:
    """
    :param html_content: s.get(list_todos_url).text
    :return: list of todo descriptions
    """
    # Parse the HTML content using Beautiful Soup
    soup = BeautifulSoup(html_content, "html.parser")

    # Find all <p> elements within the main container where todos are listed
    todos = []
    for div in soup.find_all('div', class_='todo-description'):  # TODO check if this still only filters for todos
        todos.append(div.get_text(strip=True))
    return todos


def extract_filters(html_content: str):
    soup = BeautifulSoup(html_content, "html.parser")
    # Find all <p> elements within the main container where todos are listed
    filters_dict = {}
    filters = soup.find_all('option', class_='filter')
    if not isinstance(filters, list):
        filters = [filters]
    for f in filters:
        if "value" in f.attrs:
            filters_dict[f.get_text()] = f.attrs["value"]
    return filters_dict


def error_handling(func):
    def wrapper(*args, **kwargs):
        try:
            return func(*args, **kwargs)
        except requests.exceptions.ConnectionError as ce:
            logging.error("foo: Connection error occurred.")
            return CheckResult.DOWN
        except requests.exceptions.Timeout as te:
            logging.error(f"The request timed out. {te}")
            return CheckResult.DOWN
        except requests.exceptions.HTTPError as he:
            logging.error(f"HTTP error occurred: {he}")
            return CheckResult.FAULTY
        except requests.exceptions.RequestException as e:
            logging.error(f"An error occurred: {e}")
            return CheckResult.FAULTY
        except TypeError:
            logging.error("TypeError: vulnbox might be down")
            return CheckResult.DOWN
        except:
            logging.error("Some unknown error")
            return CheckResult.DOWN

    return wrapper


class TodoItem:
    def __init__(self, description, category, is_completed=False):
        self.description = description
        self.category = category
        self.is_completed = is_completed

    def __str__(self):
        return f"Description: {self.description}, Category: {self.category}, IsCompleted: {self.is_completed}"

    def __eq__(self, other):
        if not isinstance(other, TodoItem):
            return False
        return self.description == other.description and self.category == other.category and self.is_completed == other.is_completed


class TodoListChecker(checkerlib.BaseChecker):

    def __init__(self, ip: str, team: int):
        super().__init__(ip, team)
        self.ip = ip
        self.team = team
        self.web_app_port = 8080
        self.base_url = f'http://[{ip}]:{self.web_app_port}/'
        self.register_url = self.base_url + 'Identity/Account/Register'
        self.login_url = self.base_url + 'Identity/Account/Login'
        self.list_todos_url = self.base_url + 'Todo/ListTodos'
        self.add_todo_url = self.base_url + 'Todo/AddTodo'
        self.import_url = self.base_url + 'Todo/Import'
        self.export_url = self.base_url + 'Todo/Export'
        self.add_filter_url = self.base_url + "Todo/AddFilter"
        self.apply_filter_url = self.base_url + "Todo/ApplyFilter"
        self.logout_url = self.base_url + 'Identity/Account/Logout?returnUrl=/'
        self.session = requests.Session()
        self.session.request = functools.partial(self.session.request, timeout=TIMEOUT)

        logging.info(f"ip: {ip}, team: {team}")

    def _register_user(self, email, password) -> CheckResult:
        r = self.session.get(self.register_url)
        data = {"Input.Email": email,
                "Input.Password": password,
                "Input.ConfirmPassword": password,
                "__RequestVerificationToken": extract_verification_token(r.text)}
        r2 = self.session.post(self.register_url, data=data)
        if "is already taken." in r2.text:
            # logging.info(f"{self.email} already taken")
            pass
        if r.status_code != 200 or r2.status_code != 200:
            return CheckResult.FAULTY
        return CheckResult.OK

    def _login(self, email, password) -> CheckResult:
        r = self.session.get(self.login_url)
        if not r.status_code == 200:
            logging.error(f"Couldn't login. Got response {r.status_code}")
            return CheckResult.FAULTY
        data = {"Input.Email": email,
                "Input.Password": password,
                "__RequestVerificationToken": extract_verification_token(r.text),
                "Input.RememberMe": "True"}
        r2 = self.session.post(self.login_url, data=data)
        if "Invalid login attempt." in r2.text:
            logging.error(f"{email} {password}: invalid login attempt")
            return CheckResult.FAULTY
        if r2.status_code != 200:
            logging.error(f"Error during POST request to login with login credentials {email} {password}")
            return CheckResult.FAULTY
        return CheckResult.OK

    def _add_todo(self, todo) -> CheckResult:
        r = self.session.get(self.add_todo_url)
        if self.login_url in r.url:
            logging.error(f"trying to place a new flag, but not logged in")
            return CheckResult.FAULTY
        data = {"Id": "",
                "Description": todo.description,
                "Category": todo.category,
                "IsCompleted": todo.is_completed,
                "__RequestVerificationToken": extract_verification_token(r.text)}
        r2 = self.session.post(self.add_todo_url, data)
        if r.status_code != 200 or r2.status_code != 200 or self.login_url in r2.url:
            return CheckResult.FAULTY
        return CheckResult.OK

    def _get_todos(self) -> List[str]:
        r = self.session.get(self.list_todos_url)
        return extract_todos(r.text)

    def _logout(self) -> None:
        r = self.session.get(self.list_todos_url)
        if r.status_code != 200:
            logging.warning("Could not get TODOs, therefore no  Verification Token available to logout.")
        res = self.session.post(self.logout_url,
                                data={"__RequestVerificationToken": extract_verification_token(r.text)})
        logging.info("Logged out.")

    @error_handling
    def place_flag(self, tick: int) -> CheckResult:
        res = CheckResult.FAULTY
        email = f'admin.{generate_random_string(7)}@todo-list-{generate_random_string(7)}.de'
        password = f"{generate_random_password(40, self.team, True)}"
        for _ in range(5):
            res = self._register_user(email, password)
            if res == CheckResult.OK:
                logging.info(f"Registered {email} {password} successfully at tick {tick}")
                break
            email = f'admin.{generate_random_string(5)}@todo-list-{generate_random_string(5)}.de'
            password = f"{generate_random_password(40, self.team, True)}"
        if res != CheckResult.OK:
            logging.error("Placing flag failed. Tried 5 times to register an admin account, but failed.")
            return res
        res = self._login(email, password)
        if res != CheckResult.OK:
            logging.error(f"Could not login with credentials {email} {password}")
            return res
        checkerlib.store_state(str(tick), (email, password))
        flag = checkerlib.get_flag(tick)
        res = self._add_todo(TodoItem(flag, "category", False))
        if res is not CheckResult.OK:
            logging.error(f"Could not add Todo that contains the flag {flag}")
            return res
        logger.info(f"Placed flag: {flag}")
        checkerlib.set_flagid(email)
        self._logout()
        return CheckResult.OK

    @error_handling
    def check_service(self) -> CheckResult:
        res = CheckResult.FAULTY
        for _ in range(3):
            email = get_random_email()
            password = generate_random_password(20, -1, special_char=True)
            res = self._register_user(email, password)
            if res != CheckResult.OK:
                continue
            res = self._login(email, password)
            if res == CheckResult.OK:
                break
        if res != CheckResult.OK:
            logging.error("Not able to register/login with a random account after 3 tries")
            return CheckResult.FAULTY
        description1 = generate_random_string(string_length)
        category1 = generate_random_string(string_length)
        description2 = generate_random_string(string_length)
        description3 = generate_random_string(string_length)
        category3 = generate_random_string(string_length)
        todos = [TodoItem(description1, category1),
                 TodoItem(description2, category1),
                 TodoItem(description3, category3)]
        for t in todos:
            res = self._add_todo(t)
            logging.info(f"Added {t}")
            if res != CheckResult.OK:
                logging.error(f"Error during test initialization. Application couldn't handle {t}")
                return res
        res = self._check_add_todo(todos)
        if res != CheckResult.OK:
            logging.info("Error while checking add_todo()")
            return res
        logging.info("Check Add Todo SUCCESSFULL")
        res = self._check_import()
        if res != CheckResult.OK:
            logging.info("Error while checking import()")
            return res
        logging.info("Check Import SUCCESSFULL")
        res = self._check_export(todos)
        if res != CheckResult.OK:
            logging.info("Error while checking export()")
            return res
        logging.info("Check Export SUCCESSFULL")
        res = self._check_filter(todos, category1)
        if res != CheckResult.OK:
            logging.info("Error while checking filter()")
            return res
        logging.info("Check Filter SUCCESSFULL")
        logging.info("Successfully checked service")
        self._logout()
        return CheckResult.OK

    @error_handling
    def check_flag(self, tick: int) -> CheckResult:
        try:
            email, password = checkerlib.load_state(str(tick))
        except TypeError:
            logging.error(f"Could not retrieve email and password: No data stored under key {tick}")
            return CheckResult.FLAG_NOT_FOUND
        res = self._login(email, password)
        if res != CheckResult.OK:
            logging.error(f"Could not login to check flag in tick {tick} with credentials {email} {password}")
            return CheckResult.FLAG_NOT_FOUND
        todos = self._get_todos()
        if not checkerlib.get_flag(tick) in todos:
            return CheckResult.FLAG_NOT_FOUND
        self._logout()
        return CheckResult.OK

    def _check_add_todo(self, todos_added):
        todos_available = self._get_todos()
        for t in todos_added:
            if t.description not in todos_available:
                logging.error(f"Could not find {t} while checking if it was added.")
                return CheckResult.FAULTY
        return CheckResult.OK

    def _check_import(self):
        def _handle_import(fmt, content, _descriptions):
            files = {"file": content}
            start = time.time()
            logging.info(f"POST: Importing {fmt}-string: {content}")
            _res = self.session.post(self.import_url, files=files)
            logging.info(f"Importing {fmt} took {time.time() - start}")
            if _res.status_code != 200:
                soup = BeautifulSoup(_res.text, 'html.parser')
                hidden_message = soup.find('div', id='alertMessage').text
                logging.info(f"Import returned status code {_res.status_code}: {hidden_message}")
                return CheckResult.FAULTY
            todo_descriptions = self._get_todos()
            for d in _descriptions:
                if d not in todo_descriptions:
                    logging.error(f"Missing entry from import")
                    return CheckResult.FAULTY
            return CheckResult.OK

        def _get_random_todos():
            _descriptions = [generate_random_string(string_length) for _ in range(todos_to_check)]
            category = [generate_random_string(string_length) for _ in range(todos_to_check)]
            _to_import = []
            is_completed = True
            for d, c in zip(_descriptions, category):
                _to_import.append({
                    "Category": c,
                    "Description": d,
                    "IsCompleted": is_completed
                })
                is_completed = not is_completed
            return _to_import, _descriptions

        to_import, descriptions = _get_random_todos()
        json_string = json.dumps(to_import)
        res = _handle_import("JSON", json_string, descriptions)
        if res != CheckResult.OK:
            return res
        to_import, descriptions = _get_random_todos()
        xml = dicttoxml.dicttoxml(to_import,
                                  attr_type=False,
                                  item_func=lambda x: 'TodoItem',
                                  custom_root='ArrayOfTodoItem')
        res = _handle_import("XML", xml, descriptions)
        if res != CheckResult.OK:
            return res

        return CheckResult.OK

    def _check_export(self, check_todos):
        def _completeness_check(_returned_todos):
            for _t in check_todos:
                if _t not in _returned_todos:
                    return CheckResult.FAULTY
            return CheckResult.OK

        def _get_todos(content):
            _res = []
            for _t in content:
                if "Category" in _t and "Description" in _t:  # TODO include IsCompleted
                    _res.append(TodoItem(_t["Description"], _t["Category"], False))
            return _res

        res = self.session.get(self.export_url, params={"format": "json"})
        returned_todos = _get_todos(res.json())
        if res := _completeness_check(returned_todos) != CheckResult.OK:
            logging.error("Todo missing in JSON export")
            return res
        res = self.session.get(self.export_url, params={"format": "xml"})
        d = xmltodict.parse(res.text)
        if not "ArrayOfTodoItem" in d or not "TodoItem" in d["ArrayOfTodoItem"]:
            logging.error("Invalid format returned by XML export.")
            return CheckResult.FAULTY
        returned_todos = _get_todos(d["ArrayOfTodoItem"]["TodoItem"])
        if res := _completeness_check(returned_todos) != CheckResult.OK:
            logging.error("Todo missing in XML export")
            return res
        return CheckResult.OK

    def _check_filter(self, check_todos, included_category):
        filter_name = generate_random_string(string_length)
        res = self._add_filter(filter_name, included_category)
        if res is None:
            # In this case a filter with the name already existed
            return CheckResult.OK
        if res == CheckResult.FAULTY:
            logging.error("Adding filter not successfully")
            return CheckResult.FAULTY
        todos = self.session.get(self.apply_filter_url, params={"name": filter_name})
        descriptions = extract_todos(todos.text)
        for t in check_todos:
            if t.description in descriptions:
                if t.category != included_category:
                    logging.error(
                        f"Found a Todo that must not be included in the search. Found {t}. Allowed category: {included_category}")
                    return CheckResult.FAULTY
            elif t.category == included_category:
                logging.error(f"Todo {t} was not found in the search for category {included_category}")
                return CheckResult.FAULTY
        return CheckResult.OK

    def _add_filter(self, name, category):
        payload = {"name": name,
                   "category": category}
        res = self.session.post(self.add_filter_url, data=payload)
        possible_naming_collision = False
        if res.status_code == 409:
            possible_naming_collision = True
            logging.error(f"Filter was not added, because filter with name {name} already exists.")
        if not res.status_code == 200:
            return CheckResult.FAULTY
        res = self.session.get(self.list_todos_url)
        filters = extract_filters(res.text)
        if not name in filters:
            if possible_naming_collision:
                return None
            return CheckResult.FAULTY
        return filters[name]


if __name__ == '__main__':
    checkerlib.run_check(TodoListChecker)
